using System.Globalization;

namespace Iskra.Core.Search;

/// <summary>
/// Безопасный калькулятор (без eval): + − × ÷ ^ скобки, %, mod, функции sqrt/корень, sin, cos, tan, ln, log, abs, round, константы pi/пи, e.
/// Понимает запятую как десятичный разделитель: «2,5*4».
/// </summary>
public static class Calculator
{
    public static bool LooksLikeExpression(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return false;
        if (s.StartsWith('=')) return true;
        bool digit = false, op = false;
        foreach (var c in s)
        {
            if (char.IsDigit(c)) digit = true;
            else if ("+-*/^×÷%()".IndexOf(c) >= 0) op = true;
        }
        if (!digit) return false;
        if (op) return true;
        var low = s.ToLowerInvariant();
        if (low.Contains(" mod ")) return true;
        return low.StartsWith("sqrt") || low.StartsWith("корень") || low.StartsWith("sin") || low.StartsWith("cos") || low.StartsWith("ln") || low.StartsWith("log");
    }

    public static bool TryEvaluate(string input, out double value)
    {
        value = 0;
        if (!LooksLikeExpression(input)) return false;
        try
        {
            var p = new Parser(input.Trim().TrimStart('='));
            value = p.ParseExpression();
            p.SkipWs();
            if (!p.End) return false;
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        catch (FormatException) { return false; }
    }

    public static string Format(double v, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) < 1e15) return Math.Round(v).ToString("#,0", culture);
        return v.ToString("#,0.##########", culture);
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;
        public Parser(string s) { _s = s.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace('·', '*'); }
        public bool End => _i >= _s.Length;
        public void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
        private bool Eat(char c) { SkipWs(); if (_i < _s.Length && _s[_i] == c) { _i++; return true; } return false; }
        private bool EatWord(string w)
        {
            SkipWs();
            if (string.Compare(_s, _i, w, 0, w.Length, StringComparison.OrdinalIgnoreCase) == 0) { _i += w.Length; return true; }
            return false;
        }

        public double ParseExpression()
        {
            double v = ParseTerm();
            while (true)
            {
                if (Eat('+')) v += ParseTerm();
                else if (Eat('-')) v -= ParseTerm();
                else return v;
            }
        }

        private double ParseTerm()
        {
            double v = ParseFactor();
            while (true)
            {
                if (Eat('*')) v *= ParseFactor();
                else if (Eat('/')) { var d = ParseFactor(); if (d == 0) throw new FormatException("деление на ноль"); v /= d; }
                else if (EatWord("mod")) v %= ParseFactor();
                else return v;
            }
        }

        private double ParseFactor()
        {
            double b = ParseUnary();
            if (Eat('^')) return Math.Pow(b, ParseFactor()); // правоассоциативно
            return b;
        }

        private double ParseUnary()
        {
            if (Eat('-')) return -ParseUnary();
            if (Eat('+')) return ParseUnary();
            double v = ParsePrimary();
            while (Eat('%')) v /= 100.0;
            return v;
        }

        private static readonly (string name, Func<double, double> f)[] Funcs =
        {
            ("sqrt", Math.Sqrt), ("корень", Math.Sqrt), ("sin", x => Math.Sin(x)), ("cos", Math.Cos), ("tan", Math.Tan), ("tg", Math.Tan),
            ("ln", Math.Log), ("log", Math.Log10), ("lg", Math.Log10), ("abs", Math.Abs), ("round", Math.Round), ("floor", Math.Floor), ("ceil", Math.Ceiling),
        };

        private double ParsePrimary()
        {
            SkipWs();
            if (Eat('('))
            {
                var v = ParseExpression();
                if (!Eat(')')) throw new FormatException(")");
                return v;
            }
            foreach (var (name, f) in Funcs)
            {
                int save = _i;
                if (EatWord(name))
                {
                    SkipWs();
                    if (_i < _s.Length && (_s[_i] == '(' || char.IsDigit(_s[_i]))) return f(ParseUnary());
                    _i = save;
                }
            }
            if (EatWord("pi") || EatWord("пи") || EatWord("π")) return Math.PI;
            if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'е') && (_i + 1 >= _s.Length || !char.IsLetter(_s[_i + 1]))) { _i++; return Math.E; }
            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWs();
            int start = _i;
            bool sep = false;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsDigit(c)) { _i++; continue; }
                if ((c == '.' || c == ',') && !sep && _i + 1 < _s.Length && char.IsDigit(_s[_i + 1])) { sep = true; _i++; continue; }
                if (c == ' ' && _i + 3 < _s.Length && char.IsDigit(_s[_i + 1]) && char.IsDigit(_s[_i + 2]) && char.IsDigit(_s[_i + 3]) && !sep
                    && (_i + 4 >= _s.Length || !char.IsDigit(_s[_i + 4]))) { _i++; continue; } // «1 000»
                break;
            }
            if (start == _i) throw new FormatException("число");
            var token = _s[start.._i].Replace(" ", "").Replace(',', '.');
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
