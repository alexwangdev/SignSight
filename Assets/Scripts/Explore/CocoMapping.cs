namespace SignSight.Explore
{
    /// <summary>
    /// The only place where an object label (COCO class name, used by the virtual objects now and by YOLO later) maps
    /// to a sign. A row exists only when the sign has a template in templates.json — Explore never invents a sign.
    /// </summary>
    public static class CocoMapping
    {
        public struct Entry
        {
            public string Label;   // COCO class name
            public string Intent;  // template intent, or null = not one of the 50 words
            public string Word;    // big word on the card
            public string Meaning; // line under it, says what the object is
        }

        private static readonly Entry[] Table =
        {
            new Entry { Label = "apple", Intent = "APPLE", Word = "APPLE", Meaning = "Apple" },
            new Entry { Label = "cup", Intent = "COFFEE", Word = "COFFEE", Meaning = "Cup — COFFEE" },
            new Entry { Label = "bottle", Intent = "WATER", Word = "WATER", Meaning = "Bottle — WATER" },
        };

        public const string NotInWords = "Not in my 62 words yet";

        /// <summary>Mapped entry, or an entry with Intent = null (show NotInWords, no teacher demo).</summary>
        public static Entry Lookup(string label)
        {
            foreach (var e in Table)
                if (e.Label == label) return e;
            string name = string.IsNullOrEmpty(label) ? "" : char.ToUpperInvariant(label[0]) + label.Substring(1);
            return new Entry { Label = label, Intent = null, Word = name, Meaning = NotInWords };
        }
    }
}
