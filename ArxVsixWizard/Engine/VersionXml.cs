// C# port of ArxSetupVersionSymbols() from ArxWizCommon/arxCommon.js.
// Produces the multi-year XML fragments consumed by the vcxproj skeletons.
using System.Collections.Generic;
using System.Text;
using ArxVsixWizard.Models;

namespace ArxVsixWizard.Engine
{
    public sealed class VersionFragments
    {
        public string ConfigXml { get; set; }
        public string YearRegex { get; set; }
        public string ToolsetXml { get; set; }
        public string ClrXml { get; set; }
        public string NetFrameworkCondition { get; set; }
        public string Years { get; set; }
        public List<ArxVersion> Selected { get; set; }
    }

    public static class VersionXml
    {
        static string BuildYearCondition(List<ArxVersion> years)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < years.Count; i++)
            {
                if (i > 0) sb.Append(" or ");
                sb.Append('\'').Append("$(ArxYear)'=='").Append(years[i].Year).Append('\'');
            }
            return sb.ToString();
        }

        /// <param name="clr">true for a mixed .NET module project</param>
        public static VersionFragments Build(IEnumerable<string> selectedYears, bool clr)
        {
            var selected = new List<ArxVersion>();
            foreach (var v in ArxVersionTable.Versions)
                if (selectedYears is HashSet<string> h && h.Contains(v.Year))
                    selected.Add(v);
            if (selected.Count == 0)
                throw new WizardOptionException("Select at least one target AutoCAD version on the Target Versions page.");

            // Newest year first so the newest debug configuration is the default active one.
            var ordered = new List<ArxVersion>(selected);
            ordered.Reverse();

            var config = new StringBuilder();
            foreach (var v in ordered)
            {
                config.Append("    <ProjectConfiguration Include=\"").Append(v.Year)
                      .Append("d|x64\"><Configuration>").Append(v.Year)
                      .Append("d</Configuration><Platform>x64</Platform></ProjectConfiguration>\r\n");
                config.Append("    <ProjectConfiguration Include=\"").Append(v.Year)
                      .Append("|x64\"><Configuration>").Append(v.Year)
                      .Append("</Configuration><Platform>x64</Platform></ProjectConfiguration>\r\n");
            }

            var regex = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0) regex.Append('|');
                regex.Append(selected[i].Year);
            }

            var toolset = new StringBuilder();
            foreach (var v in selected)
            {
                toolset.Append("    <PlatformToolset Condition=\"'$(ArxYear)'=='")
                       .Append(v.Year).Append("'\">").Append(v.Toolset)
                       .Append("</PlatformToolset>\r\n");
            }

            string clrXml;
            string netFwCond = "";
            if (!clr)
            {
                clrXml = "<CLRSupport>false</CLRSupport>";
            }
            else
            {
                var core = new List<ArxVersion>();
                var framework = new List<ArxVersion>();
                foreach (var v in selected)
                    if (v.ClrCore) core.Add(v); else framework.Add(v);

                var sb = new StringBuilder();
                if (framework.Count > 0)
                {
                    string cond = BuildYearCondition(framework);
                    sb.Append("<CLRSupport Condition=\"").Append(cond).Append("\">true</CLRSupport>");
                    // The framework ObjectARX net property sheets do not reference System.Core,
                    // but mgdinterop.h uses System.Dynamic (IDynamicMetaObjectProvider).
                    netFwCond = cond;
                }
                if (core.Count > 0)
                    sb.Append("<CLRSupport Condition=\"").Append(BuildYearCondition(core)).Append("\">NetCore</CLRSupport>");
                clrXml = sb.ToString();
            }

            var years = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0) years.Append(',');
                years.Append(selected[i].Year);
            }

            return new VersionFragments
            {
                ConfigXml = config.ToString(),
                YearRegex = regex.ToString(),
                ToolsetXml = toolset.ToString(),
                ClrXml = clrXml,
                NetFrameworkCondition = netFwCond,
                Years = years.ToString(),
                Selected = selected
            };
        }
    }

    public sealed class WizardOptionException : System.Exception
    {
        public WizardOptionException(string message) : base(message) { }
    }
}
