using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace PrescriptionSignatureVerifier
{
    internal class VerificationHelper
    {
        internal static List<Verification> LoadVerifications(string f)
        {
            // Open file f. Read into a string. See if it is a prescription (PORX_IN01...) or a release response (PORX_IN07)
            // and if it is "bare xml" or wrapped in something.
            // 1. If wrapped, extract the bare xml
            // 2. If a prescription, make a verification and parse
            // 3. If a release response make a Verification for each contained prescription

            List<Verification> l = new List<Verification>();
            string content = null;
            using (TextReader tr = File.OpenText(f))
            {
                content = tr.ReadToEnd();
            }
            if (content.Contains("PORX_IN0201"))
            {
                DoPrescription(l, content, f);
            } else
            {
                if (content.Contains("PORX_IN070"))
                {
                    DoReleaseResponse(l, content, f);
                } else
                {
                    Verification v = new Verification(f)
                    {
                        Result = "FAILURE: Cannot read prescription(s)"
                    };
                    l.Add(v);
                }
            }
            return l;
        }

        private static void DoPrescription(List<Verification> l, string c, string f)
        {
            string hl7 = Unwrap(c);
            Verification v = new Verification(f, hl7);
            l.Add(v);
        }

        private static void DoReleaseResponse(List<Verification> l, string c, string f)
        {
            string hl7 = Unwrap(c);

            // Need to make a separate Verification for each ParentPrescription
            //
            XmlDocument d = new XmlDocument();
            d.LoadXml(hl7);
            XmlNodeList nl = d.GetElementsByTagName("ParentPrescription", "urn:hl7-org:v3");
            for (int i = 0; i < nl.Count; i++)
            {
                string s = ((XmlElement)nl.Item(i)).OuterXml;
                Verification v = new Verification(f, s);
                l.Add(v);
            }
        }

        private static int FindStartOfXmlTag(string c, int i)
        {
            int j = i;
            char[] characters = c.ToCharArray();
            while (j > -1)
            {
                if (characters[j] == '<')
                    return j;
                j--;
            }
            throw new Exception("Unable to find XML start");
        }

        private static string Unwrap(string c)
        {
            if (c.StartsWith("<"))
            {
                // See if we can just read it and if so, return c... otherwise carry on
                //
                try
                {
                    XmlDocument d = new XmlDocument();
                    d.LoadXml(c);
                    return c;
                }
                catch { }
            }
            int h = GetHL7Start(c);
            string p = GetHL7EndTag(c, h);
            return p;
        }

        private static string GetHL7EndTag(string c, int h)
        {
            StringBuilder sb = new StringBuilder();
            int i = h;
            char[] characters = c.ToCharArray();
            bool addslash = true;
            while ((characters[i] != '>') && (characters[i] != ' '))
            {
                sb.Append(characters[i]);
                if (addslash)
                {
                    sb.Append("/");
                    addslash = false;
                }
                i++;
            }
            sb.Append(">");
            string tag = sb.ToString();
            int etag = c.IndexOf(tag) + tag.Length;
            string p = c.Substring(h, etag - h);
            return p;
        }

        private static int GetHL7Start(string c)
        {
            int i = c.IndexOf("<PORX_");
            if (i == -1)
            {
                i = c.IndexOf(":PORX");
            }
            if (i == -1)
            {
                throw new Exception("Start of HL7v3 payload (<PORX or :PORX) not found");
            }
            return FindStartOfXmlTag(c, i);
        }
    }
}
