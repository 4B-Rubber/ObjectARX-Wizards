// C# port of CreateSafeName() from the Visual Studio common.js wizard helper.
using System.Text;

namespace ArxVsixWizard.Engine
{
    public static class Names
    {
        /// <summary>Keeps [A-Za-z0-9_], skips other characters; prepends "My" if empty or digit-led.</summary>
        public static string SafeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_' ||
                    (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                }
            }
            string s = sb.ToString();
            if (s.Length == 0)
                s = "My";
            else if (s[0] >= '0' && s[0] <= '9')
                s = "My" + s;
            return s;
        }
    }
}
