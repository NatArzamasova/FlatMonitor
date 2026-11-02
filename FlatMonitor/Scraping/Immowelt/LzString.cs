namespace FlatMonitor.Scraping.Immowelt;

using System.Text;

/// <summary>
/// <c>LZString.decompressFromBase64</c> of the JavaScript library lz-string (pieroxy), which immowelt uses to compress
/// the search results embedded in its list pages.
/// </summary>
public static class LzString
{
    private const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

    /// <summary>The decompressed text; <c>""</c> for empty input, <c>null</c> if the input is no lz-string data.</summary>
    public static string? DecompressFromBase64(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        var values = new int[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            values[i] = Base64Alphabet.IndexOf(input[i]);
            if (values[i] < 0)
                return null;
        }

        return Decompress(new BitReader(values, resetValue: 32));
    }

    private static string? Decompress(BitReader reader)
    {
        var dictionary = new List<string> { "", "", "" };   // codes 0-2 are control codes
        int enlargeIn = 4;
        int numBits = 3;

        string? Literal(int code) => code switch
        {
            0 => ((char)reader.Read(8)).ToString(),
            1 => ((char)reader.Read(16)).ToString(),
            _ => null,
        };

        var first = Literal(reader.Read(2));
        if (first == null)
            return "";

        dictionary.Add(first);
        var previous = first;
        var result = new StringBuilder(first);

        while (true)
        {
            if (reader.IsPastEnd)
                return null;

            int code = reader.Read(numBits);
            if (code == 2)
                return result.ToString();

            if (code is 0 or 1)
            {
                dictionary.Add(Literal(code)!);
                code = dictionary.Count - 1;
                if (--enlargeIn == 0)
                    enlargeIn = 1 << numBits++;
            }

            string entry;
            if (code < dictionary.Count)
                entry = dictionary[code];
            else if (code == dictionary.Count)
                entry = previous + previous[0];
            else
                return null;

            result.Append(entry);
            dictionary.Add(previous + entry[0]);
            previous = entry;

            if (--enlargeIn == 0)
                enlargeIn = 1 << numBits++;
        }
    }

    /// <summary>Reads the input's values bit by bit, highest bit first, in groups of up to 16 bits.</summary>
    private sealed class BitReader
    {
        private readonly int[] _values;
        private readonly int _resetValue;
        private int _index = 1;
        private int _position;
        private int _value;

        public BitReader(int[] values, int resetValue)
        {
            _values = values;
            _resetValue = resetValue;
            _position = resetValue;
            _value = values[0];
        }

        /// <summary>A stream without an end code has run out: the input was cut off.</summary>
        public bool IsPastEnd => _index > _values.Length;

        public int Read(int bitCount)
        {
            int bits = 0;
            for (int power = 0; power < bitCount; power++)
            {
                int bit = _value & _position;
                _position >>= 1;
                if (_position == 0)
                {
                    _position = _resetValue;
                    _value = _index < _values.Length ? _values[_index] : 0;
                    _index++;
                }

                if (bit != 0)
                    bits |= 1 << power;
            }

            return bits;
        }
    }
}
