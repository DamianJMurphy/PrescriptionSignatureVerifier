using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace PrescriptionSignatureVerifier
{
    internal class Verification
    {
        private readonly string filename = null;
        private readonly XmlElement rx = null;
        private string id = null;
        private X509Certificate2 certificate = null;
        private string certdetails = null;
        private string result = null;
        private bool hasSHA256Hash = false;

        internal Verification(string f, string p)
        {
            filename = f;
            rx = Process(p);
        }

        internal Verification(string f)
        {
            filename = f;
        }

        internal bool CanProceed() { return (id != null) && (certificate != null); }
        public string Certdetails => certdetails;
        public string Id => id;

        public string Filename => filename;

        public string Result { get => result; set => result = value; }
        public bool HasSHA256Hash => hasSHA256Hash;

        internal XmlElement GetParentPrescription() { return rx; }
        internal X509Certificate2 GetCertificate() { return certificate;  }

        private XmlElement Process(string p)
        {
            hasSHA256Hash = p.Contains("http://www.w3.org/2001/04/xmlenc#sha256");
            XmlDocument d = new XmlDocument();
            d.LoadXml(p);
            XmlNodeList rxn = d.GetElementsByTagName("ParentPrescription");
            if (rxn.Count == 0)
            {
                Result = " No ParentPrescription element found";
                return null;
            }
            XmlElement pp = (XmlElement)rxn.Item(0);   
            XmlNodeList nl = pp.GetElementsByTagName("id");
            id = "Not found";
            for (int i = 0; i < nl.Count; i++)
            {
                XmlElement pid = (XmlElement)nl.Item(i);
                string oid = pid.GetAttribute("root");
                if (oid.Equals("2.16.840.1.113883.2.1.3.2.4.18.8"))
                {
                    id = pid.GetAttribute("extension");
                    break;
                }
            }
            ExtractCertificate(pp);
            certdetails = GetCertificateDetails();
            return pp;
        }

        private void ExtractCertificate(XmlElement k)
        {
            XmlNodeList nl = k.GetElementsByTagName("X509Data", "http://www.w3.org/2000/09/xmldsig#");
            if (nl.Count == 0)
            {
                result = " Invalid, X509Data (certificate) not found";
                return;
            }
            XmlElement xc = (XmlElement)nl.Item(0);
            try
            {
                certificate = new X509Certificate2(Convert.FromBase64String(xc.InnerText));
            }
            catch (Exception)
            {
                result = " FAILED: Invalid or corrupt X509 certificate";
            }
        }

        private string GetCertificateDetails()
        {
            if (certificate == null)
            {
                return "No certificate found";
            }
            StringBuilder sb = new StringBuilder("Certificate: Subject ");
            sb.Append(certificate.Subject);
            sb.Append(" Issuer ");
            sb.Append(certificate.Issuer);
            sb.Append(" Algorithm ");
            sb.Append(certificate.SignatureAlgorithm.FriendlyName);
            sb.Append(" Not Before ");
            sb.Append(certificate.GetEffectiveDateString());
            sb.Append(" Not After ");
            sb.Append(certificate.GetExpirationDateString());
            sb.Append("\r\n");
            return sb.ToString();
        }
    }
}
