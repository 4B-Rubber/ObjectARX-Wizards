// Resource side effects of the ObjectARX MFC class wizard (RunPostActions).
//
// Port of OnFinish() in ArxWizMFCSupport\Scripts\1033\default.js:38-72: when the user
// asked for a new dialog, the chosen template (dialog.rc or, for child-window base
// classes, childDialog.rc) is appended to the project's .rc file and a fresh ID is
// allocated in Resource.h. Every failure is logged instead of thrown.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ArxVsixWizard.Items
{
    public static class MfcResourceEditor
    {
        const string DialogRc = "ArxWizMfcSupport.dialog.rc";
        const string ChildDialogRc = "ArxWizMfcSupport.childDialog.rc";

        static readonly Encoding Ansi = Encoding.GetEncoding(1252);

        public static void Apply(ItemModel model, ItemContext context)
        {
            try
            {
                if (model == null || context == null) return;

                string id = model.Symbols.GetString("IDD_DIALOG").Trim();
                string dir = context.TargetDir;
                // Unconditional trace: this step is invisible otherwise, and a silent skip looks
                // exactly like a broken wizard in the IDE.
                Note("start: id=" + (id.Length == 0 ? "<empty>" : id)
                     + " dir=" + (string.IsNullOrEmpty(dir) ? "<unknown>" : dir)
                     + " createDialog=" + model.Symbols.GetBool("CREATE_DIALOG")
                     + " project=" + (context.Project?.Name ?? "<null>"));

                if (id.Length == 0)
                {
                    Note("dialog resource skipped: IDD_DIALOG is empty");
                    return;
                }

                bool child = !string.IsNullOrEmpty(model.Symbols.GetString("CHILD_DIALOG_NEEDED"))
                          || !string.IsNullOrEmpty(model.Symbols.GetString("CHILD_RESOURCE_NEEDED"));
                string template = ArxItemWizardBase.ReadResource(child ? ChildDialogRc : DialogRc);

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    Note("dialog resource skipped: project directory unknown");
                    return;
                }

                string rcPath = FirstRcFile(dir);
                string resPath = ResourceHeaderPath(dir);

                // The decision is "does the project already know this id", which is what the old
                // wizard's DialogList check did. The CREATE_DIALOG checkbox is only a hint - making
                // it a hard requirement silently produced classes referencing undefined ids.
                if (resPath != null && IdAlreadyDefined(resPath, id))
                {
                    Note("dialog resource: " + id + " is already defined, nothing to add");
                    return;
                }

                if (rcPath == null || resPath == null)
                {
                    // Do not touch anything existing: emit standalone resource files instead.
                    Note("dialog resource: no usable .rc/Resource.h in " + dir + ", writing standalone files");
                    WriteStandalone(dir, model, id, template);
                    return;
                }

                int value = ReadNextResourceValue(resPath);
                File.AppendAllText(rcPath, "\r\n" + NameResource(template, id) + "\r\n", Ansi);
                UpdateResourceHeader(resPath, id, value);
                Note("dialog resource: " + id + " = " + value + " appended to " + rcPath);
            }
            catch (Exception ex)
            {
                ItemContext.Log("MfcResourceEditor.Apply", ex);
            }
        }

        /// <summary>The .rc that belongs to the project (skips the OMF resource sub-project under Enu\).</summary>
        static string FirstRcFile(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.rc", SearchOption.TopDirectoryOnly))
                return file;
            return null;
        }

        /// <summary>resource.h / Resource.h in the project directory, whatever its casing.</summary>
        static string ResourceHeaderPath(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.h", SearchOption.TopDirectoryOnly))
                if (string.Equals(Path.GetFileName(file), "resource.h", StringComparison.OrdinalIgnoreCase))
                    return file;
            return null;
        }

        static bool IdAlreadyDefined(string resPath, string id)
            => Regex.IsMatch(ReadAllText(resPath),
                @"^\s*#define\s+" + Regex.Escape(id) + @"\b", RegexOptions.Multiline);

        /// <summary>Turns the unnamed template ("DIALOG 0, 0, ...") into "&lt;id&gt; DIALOG ...".</summary>
        static string NameResource(string template, string id)
        {
            string text = template.Replace("\r\n", "\n");
            int nl = text.IndexOf('\n');
            string first = nl < 0 ? text : text.Substring(0, nl);
            string rest = nl < 0 ? null : text.Substring(nl + 1);

            string trimmed = first.TrimStart();
            string renamed;
            if (trimmed.StartsWith("DIALOG", StringComparison.OrdinalIgnoreCase))
            {
                renamed = id + " " + trimmed;
            }
            else
            {
                int sp = trimmed.IndexOfAny(new[] { ' ', '\t' });
                renamed = sp < 0 ? id + " " + trimmed : id + trimmed.Substring(sp);
            }

            string result = rest == null ? renamed : renamed + "\n" + rest;
            return result.Replace("\n", "\r\n");
        }

        static int ReadNextResourceValue(string resPath)
        {
            string text = ReadAllText(resPath);
            var m = Regex.Match(text, @"#define\s+_APS_NEXT_RESOURCE_VALUE\s+(\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int value))
                return value;

            // No bookkeeping define: stay above every explicit value already in the file.
            int max = 99;
            foreach (Match d in Regex.Matches(text, @"#define\s+\w+\s+(\d+)\s*$", RegexOptions.Multiline))
                if (int.TryParse(d.Groups[1].Value, out int n) && n > max) max = n;
            return max + 1;
        }

        static void UpdateResourceHeader(string resPath, string id, int value)
        {
            string text = ReadAllText(resPath);

            if (!Regex.IsMatch(text, @"^\s*#define\s+" + Regex.Escape(id) + @"\b", RegexOptions.Multiline))
                text = ResourceHeader.InsertDefine(text, "#define " + id + " " + value);

            var m = Regex.Match(text, @"(#define\s+_APS_NEXT_RESOURCE_VALUE\s+)(\d+)");
            if (m.Success)
                text = text.Substring(0, m.Groups[2].Index) + (value + 1)
                     + text.Substring(m.Groups[2].Index + m.Groups[2].Length);
            else
                text = text.TrimEnd() + "\r\n#define _APS_NEXT_RESOURCE_VALUE " + (value + 1) + "\r\n";

            WriteAllText(resPath, text);
        }

        static void WriteStandalone(string dir, ItemModel model, string id, string template)
        {
            string className = model.Symbols.GetString("CLASS_NAME").Trim();
            if (className.Length == 0) className = "MyDialog";

            const int value = 1000;
            string headerName = className + "Resource.h";
            WriteAllText(Path.Combine(dir, headerName), "#define " + id + " " + value + "\r\n");

            string rc = "#include \"" + headerName + "\"\r\n\r\n" + NameResource(template, id) + "\r\n";
            WriteAllText(Path.Combine(dir, className + ".rc"), rc);
        }

        static string ReadAllText(string path)
        {
            using (var reader = new StreamReader(path, Ansi))
                return reader.ReadToEnd();
        }

        static void WriteAllText(string path, string text)
        {
            File.WriteAllText(path, text, Ansi);
        }

        static void Note(string message)
        {
            // ItemContext only logs exceptions, so the diagnostic rides along as one.
            ItemContext.Log("MfcResourceEditor", new InvalidOperationException(message));
        }
    }
}