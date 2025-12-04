using System;

namespace SigCoreLauncher {
    public static class VersionComparer {
        public static int Compare(string a, string b) {
            if (a == null || b == null)
                return 0;

            string[] pa = a.Split('.');
            string[] pb = b.Split('.');

            int len = Math.Max(pa.Length, pb.Length);

            for (int i = 0; i < len; i++) {
                int ai = (i < pa.Length) ? ParseOrZero(pa[i]) : 0;
                int bi = (i < pb.Length) ? ParseOrZero(pb[i]) : 0;

                if (ai < bi) return -1;
                if (ai > bi) return 1;
            }

            return 0;
        }

        private static int ParseOrZero(string s) {
            int value;
            if (Int32.TryParse(s, out value))
                return value;

            return 0;
        }
    }
}
