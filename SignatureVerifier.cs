using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Xsl;
using System.Xml;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PrescriptionSignatureVerifier
{
    internal class SignatureVerifier
    {
        private const string FRAGMENTEXTRACTOR = "PrescriptionSignatureVerifier.Prescriptionv0r1.xslt";
        private readonly XslCompiledTransform fragmentExtractor = null;

        private readonly string fileName = null;

        private readonly List<Verification> jobs = new List<Verification>();

        internal SignatureVerifier(string rxfile)
        {
            fileName = rxfile;
            jobs = VerificationHelper.LoadVerifications(fileName);
            fragmentExtractor = new XslCompiledTransform();
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(FRAGMENTEXTRACTOR);
            fragmentExtractor.Load(XmlReader.Create(s));
        }

        public string GetResult()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Verification v in jobs)
            {
                sb.Append(v.Filename);
                sb.Append("\r\n");
                sb.Append(v.Certdetails);
                sb.Append("RxID: ");
                sb.Append(v.Id);
                sb.Append(" Result: ");
                sb.Append(v.Result);
                sb.Append("\r\n-------------------\r\n");
            }
            return sb.ToString();
        }

        internal void Verify()
        {
            foreach (Verification v in jobs)
            {
                // 1. Get Signature and Certificate, and check them
                // 2. Run transform, canonicalise and hash, check against digest value and report yes/no
                // 3. Extract SignatureData, convert to byte array
                // 4. Extract and canonicalise SignedInfo, convert to byte arrya
                // 5. Run VerifyData
                string result;
                try
                {
                    if (v.CanProceed())
                    {
                        XmlElement sig = GetSignature(v);
                        XmlElement si = GetSignedInfo(v);
                        VerifyGivenDigest(v, si);
                        VerifySignature(v, sig, si);
                        result = "SUCCESS";
                    } else
                    {
                        throw new Exception(v.Result);
                    }
                }
                catch (Exception e)
                {
                    result = "FAILED: " + e.Message;
                }
                v.Result = result;
            }
        }

        private void VerifySignature(Verification v, XmlElement sig, XmlElement si)
        {
            byte[] bsv = ExtractSignatureValue(sig);
            byte[] bsi = GetSignedInfoForVerification(si);
            CheckSignature(v, bsv, bsi);
        }

        private void CheckSignature(Verification v, byte[] bsv, byte[] bsi)
        {
            RSACryptoServiceProvider rsa = (RSACryptoServiceProvider)v.GetCertificate().PublicKey.Key;
            bool b;
            if (v.HasSHA256Hash)
            {
                b = rsa.VerifyData(bsi, new SHA256CryptoServiceProvider(), bsv);
            }
            else
            {
                b = rsa.VerifyData(bsi, new SHA1CryptoServiceProvider(), bsv);
            }
            if (!b)
            {
                throw new Exception("Signature check failed");
            }
        }

        private byte[] GetSignedInfoForVerification(XmlElement si)
        {
            XmlDsigExcC14NTransform canon = new XmlDsigExcC14NTransform(false);
            XmlDocument d = new XmlDocument();
            d.AppendChild(d.ImportNode(si, true));
            canon.LoadInput(d);
            byte[] csi;
            using (MemoryStream ms = new MemoryStream())
            {
                ((Stream)canon.GetOutput()).CopyTo(ms);
                csi = ms.GetBuffer();
            }
            return csi;
        }

        private byte[] ExtractSignatureValue(XmlElement signature)
        {
            string s = GetSignatureValue(signature);
            return Convert.FromBase64String(s);
        }

        private void VerifyGivenDigest(Verification v, XmlElement si)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = false,
                CloseOutput = true,
                Encoding = Encoding.UTF8,
                NewLineChars = "\n"
            };
            XmlWriter xw = XmlWriter.Create(sb, settings);
            fragmentExtractor.Transform(XmlReader.Create(new StringReader(v.GetParentPrescription().OuterXml)), null, xw);
            XmlDocument fragments = new XmlDocument();
            fragments.LoadXml(sb.ToString());
            XmlDsigExcC14NTransform canon = new XmlDsigExcC14NTransform(false);
            canon.LoadInput(fragments);
            StreamReader sr = new StreamReader((Stream)canon.GetOutput());
            string xf = sr.ReadToEnd();
            byte[] fragmentHash;
            if (v.HasSHA256Hash)
            {
                fragmentHash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(xf));
            } else
            {
                fragmentHash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(xf));
            }
            string fh64 = Convert.ToBase64String(fragmentHash);
            XmlNodeList nl = si.GetElementsByTagName("DigestValue", "http://www.w3.org/2000/09/xmldsig#");
            if (nl.Count == 0)
            {
                throw new Exception("DigestValue element not found");
            }
            string c = ((XmlElement)nl.Item(0)).InnerText;
            if (!c.Equals(fh64))
            {
                throw new Exception("Digest match failure");
            }
        }




        private string GetSignatureValue(XmlElement sig)
        {
            XmlNodeList nl = sig.GetElementsByTagName("SignatureValue", "http://www.w3.org/2000/09/xmldsig#");
            if (nl.Count == 0)
            {
                throw new Exception("Invalid, cannot find SignatureValue element");
            }
            return nl.Item(0).InnerText;
        }

        private XmlElement GetSignature(Verification v)
        {
            XmlNodeList nl = v.GetParentPrescription().GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#");
            if (nl.Count == 0)
            {
                throw new Exception("Invalid, cannot find Signature element");
            }
            return (XmlElement)nl.Item(0);
        }

        private XmlElement GetSignedInfo(Verification v)
        {
            XmlNodeList nl = v.GetParentPrescription().GetElementsByTagName("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
            if (nl.Count == 0)
            {
                throw new Exception("Invalid, cannot find SignedInfo element");
            }
            return (XmlElement)nl.Item(0);
        }

    }
}
