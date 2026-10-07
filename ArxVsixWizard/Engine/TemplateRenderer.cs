// Renders the classic Visual C++ wizard template directives:
//   [!if EXPR] ... [!else] ... [!endif]   (may occupy their own line, or run inline)
//   [!output SYMBOL]                       (inline substitution)
// EXPR supports: SYMBOL, !SYMBOL, parentheses, && and || (&& binds tighter than ||).
using System;
using System.Collections.Generic;
using System.Text;

namespace ArxVsixWizard.Engine
{
    public sealed class SymbolTable
    {
        readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Set(string name, bool value) => _bools[name] = value;
        public void Set(string name, string value)
        {
            _strings[name] = value;
            // every defined string symbol is also "truthy" for [!if] unless explicitly set as bool
            if (!_bools.ContainsKey(name)) _bools[name] = !string.IsNullOrEmpty(value);
        }

        public bool GetBool(string name) => _bools.TryGetValue(name, out var v) && v;
        public string GetString(string name) => _strings.TryGetValue(name, out var v) ? v : "";
    }

    public static class TemplateRenderer
    {
        public static string Render(string template, SymbolTable symbols)
        {
            var lines = template.Replace("\r\n", "\n").Split('\n');
            var outLines = new List<string>(lines.Length);
            // Stack frames: (parentActive, branchTaken, currentBranchActive)
            var stack = new Stack<Frame>();

            foreach (var raw in lines)
            {
                string trimmed = raw.Trim();

                // A line holding exactly one if/else/endif directive emits no text at all.
                if (IsWholeLineDirective(trimmed, out string directive))
                {
                    ApplyDirective(directive, symbols, stack);
                    continue;
                }

                // Lines without any conditional directive keep the original fast path so that
                // the project templates render byte-for-byte identically.
                if (!HasInlineDirective(raw))
                {
                    if (Active(stack))
                        outLines.Add(Substitute(raw, symbols));
                    continue;
                }

                // Inline [!if]/[!else]/[!endif] mixed with text (ATL templates).
                string text = RenderInline(raw, symbols, stack);
                if (text.Length > 0)
                    outLines.Add(text);
            }
            if (stack.Count != 0) throw new InvalidOperationException("Unbalanced [!if] in template");
            return string.Join("\r\n", outLines);
        }

        /// <summary>Doubles '$' so the VS template engine does not re-expand injected content (C4).</summary>
        public static string EscapeDollars(string text)
            => text == null ? null : text.Replace("$", "$$");

        static bool Active(Stack<Frame> stack)
        {
            foreach (var f in stack) if (!f.Active) return false;
            return true;
        }

        static bool IsWholeLineDirective(string trimmed, out string directive)
        {
            directive = null;
            if (trimmed.Length < 3 || !trimmed.StartsWith("[!", StringComparison.Ordinal)
                || !trimmed.EndsWith("]", StringComparison.Ordinal))
                return false;
            // Only one token on the line: the first ']' is also the last character.
            if (trimmed.IndexOf(']') != trimmed.Length - 1)
                return false;
            string d = trimmed.Substring(2, trimmed.Length - 3).Trim();
            if (!(d.StartsWith("if ", StringComparison.Ordinal) ||
                  d.StartsWith("if(", StringComparison.Ordinal) ||
                  d == "else" || d == "endif"))
                return false;
            directive = d;
            return true;
        }

        static bool HasInlineDirective(string line)
        {
            return line.IndexOf("[!if", StringComparison.Ordinal) >= 0
                || line.IndexOf("[!else", StringComparison.Ordinal) >= 0
                || line.IndexOf("[!endif", StringComparison.Ordinal) >= 0;
        }

        static void ApplyDirective(string directive, SymbolTable symbols, Stack<Frame> stack)
        {
            if (directive.StartsWith("if ", StringComparison.Ordinal) ||
                directive.StartsWith("if(", StringComparison.Ordinal))
            {
                PushIf(directive.Substring(2).Trim(), symbols, stack);
            }
            else if (directive == "else")
            {
                ApplyElse(stack);
            }
            else if (directive == "endif")
            {
                ApplyEndif(stack);
            }
            else
            {
                throw new InvalidOperationException("Unknown directive: " + directive);
            }
        }

        static void PushIf(string expr, SymbolTable symbols, Stack<Frame> stack)
        {
            bool parentActive = Active(stack);
            bool taken = parentActive && Expression.Eval(expr, symbols);
            stack.Push(new Frame { ParentActive = parentActive, AnyBranchTaken = taken, Active = taken });
        }

        static void ApplyElse(Stack<Frame> stack)
        {
            if (stack.Count == 0) throw new InvalidOperationException("[!else] without [!if]");
            var f = stack.Peek();
            f.Active = f.ParentActive && !f.AnyBranchTaken;
            if (f.Active) f.AnyBranchTaken = true;
        }

        static void ApplyEndif(Stack<Frame> stack)
        {
            if (stack.Count == 0) throw new InvalidOperationException("[!endif] without [!if]");
            stack.Pop();
        }

        /// <summary>Walks a line that mixes text with [!if]/[!else]/[!endif]/[!output] tokens.</summary>
        static string RenderInline(string line, SymbolTable symbols, Stack<Frame> stack)
        {
            var sb = new StringBuilder(line.Length);
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '[' && i + 1 < line.Length && line[i + 1] == '!')
                {
                    int end = line.IndexOf(']', i + 2);
                    if (end < 0) { sb.Append(line[i++]); continue; }
                    string token = line.Substring(i + 2, end - i - 2).Trim();
                    if (token.StartsWith("if ", StringComparison.Ordinal) ||
                        token.StartsWith("if(", StringComparison.Ordinal))
                    {
                        PushIf(token.Substring(2).Trim(), symbols, stack);
                        i = end + 1;
                        continue;
                    }
                    if (token == "else") { ApplyElse(stack); i = end + 1; continue; }
                    if (token == "endif") { ApplyEndif(stack); i = end + 1; continue; }
                    if (token.StartsWith("output ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Active(stack)) sb.Append(symbols.GetString(token.Substring(7).Trim()));
                        i = end + 1;
                        continue;
                    }
                    // Unrecognised token: emit literally (the smoke test asserts none remain).
                    sb.Append(line[i++]);
                    continue;
                }
                if (Active(stack)) sb.Append(line[i]);
                i++;
            }
            return sb.ToString();
        }

        static string Substitute(string line, SymbolTable symbols)
        {
            var sb = new StringBuilder(line.Length);
            int i = 0;
            while (i < line.Length)
            {
                if (i + 7 < line.Length && line[i] == '[' && line[i + 1] == '!' &&
                    line.Substring(i).StartsWith("[!output ", StringComparison.OrdinalIgnoreCase))
                {
                    int end = line.IndexOf(']', i + 8);
                    if (end < 0) { sb.Append(line[i++]); continue; }
                    string name = line.Substring(i + 8, end - i - 8).Trim();
                    sb.Append(symbols.GetString(name));
                    i = end + 1;
                }
                else
                {
                    sb.Append(line[i++]);
                }
            }
            return sb.ToString();
        }

        class Frame
        {
            public bool ParentActive;
            public bool AnyBranchTaken;
            public bool Active;
        }
    }

    /// <summary>Minimal boolean expression parser: expr := or, or := and ('||' and)*, and := unary ('&&' unary)*.</summary>
    internal static class Expression
    {
        static string _s;
        static int _p;

        public static bool Eval(string text, SymbolTable symbols)
        {
            _s = text;
            _p = 0;
            bool v = ParseOr(symbols);
            SkipSpaces();
            if (_p != _s.Length) throw new InvalidOperationException("Cannot parse directive expression: " + text);
            return v;
        }

        static void SkipSpaces() { while (_p < _s.Length && char.IsWhiteSpace(_s[_p])) _p++; }

        static bool ParseOr(SymbolTable sym)
        {
            bool v = ParseAnd(sym);
            for (; ; )
            {
                SkipSpaces();
                if (Match("||")) v = v | ParseAnd(sym);
                else return v;
            }
        }

        static bool ParseAnd(SymbolTable sym)
        {
            bool v = ParseUnary(sym);
            for (; ; )
            {
                SkipSpaces();
                if (Match("&&")) v = v & ParseUnary(sym);
                else return v;
            }
        }

        static bool ParseUnary(SymbolTable sym)
        {
            SkipSpaces();
            if (Match("!")) return !ParseUnary(sym);
            if (Match("("))
            {
                bool v = ParseOr(sym);
                SkipSpaces();
                if (!Match(")")) throw new InvalidOperationException("Missing ')' in directive expression");
                return v;
            }
            int start = _p;
            while (_p < _s.Length && (char.IsLetterOrDigit(_s[_p]) || _s[_p] == '_')) _p++;
            if (_p == start) throw new InvalidOperationException("Expected symbol in directive expression: " + _s);
            return sym.GetBool(_s.Substring(start, _p - start));
        }

        static bool Match(string token)
        {
            if (_p + token.Length <= _s.Length && _s.Substring(_p, token.Length) == token)
            {
                _p += token.Length;
                return true;
            }
            return false;
        }
    }
}