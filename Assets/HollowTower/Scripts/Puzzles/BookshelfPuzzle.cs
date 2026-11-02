using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The library's secret bookcase (floor 2): eight books with bright spines stand among the dull ones. A card on the
    /// librarian's desk names four colours in riddles, in order. Pull those four books in that order and the whole
    /// bookcase swings out on a hinge - the fuse cell is in the niche behind it. Pull a wrong one and every book thumps
    /// back into place (loud: the party guests hear it).
    /// </summary>
    public class BookshelfPuzzle : WorldPuzzle
    {
        public Transform[] books;
        public string[] colourNames;
        public TextMesh clueCard;
        public Transform bookcase;
        public float pullDistance = 0.14f;
        public int length = 4;
        [Tooltip("Degrees the case swings out on its hinge (sign: away from the wall).")]
        public float swing = -75f;

        static readonly string[] Riddles =
        {
            "red", "the colour of a stop sign",
            "blue", "the colour of a clear sky",
            "green", "the colour of new grass",
            "yellow", "the colour of a lemon",
            "purple", "the colour of a bruise",
            "orange", "the colour of a pumpkin",
            "white", "the colour of chalk dust",
            "black", "the colour of a blackboard",
        };

        int[] order; int progress;
        bool[] pulled;
        Vector3[] rest;
        Quaternion caseClosed; float caseT;

        protected override void Init()
        {
            base.Init();
            rest = new Vector3[books.Length];
            for (int i = 0; i < books.Length; i++) rest[i] = books[i].localPosition;
            pulled = new bool[books.Length];
            if (bookcase != null) caseClosed = bookcase.localRotation;
        }

        protected override void Setup(System.Random rnd)
        {
            var idx = new System.Collections.Generic.List<int>();
            for (int i = 0; i < books.Length; i++) idx.Add(i);
            order = new int[Mathf.Min(length, books.Length)];
            for (int i = 0; i < order.Length; i++) { int k = rnd.Next(idx.Count); order[i] = idx[k]; idx.RemoveAt(k); }
            if (clueCard != null)
            {
                var sb = new System.Text.StringBuilder("PULL, IN ORDER:\n");
                for (int i = 0; i < order.Length; i++) sb.Append((i + 1) + ". " + Riddle(colourNames[order[i]]) + "\n");
                clueCard.text = sb.ToString();
            }
        }

        static string Riddle(string colour)
        {
            for (int i = 0; i < Riddles.Length; i += 2) if (Riddles[i] == colour) return Riddles[i + 1];
            return colour;
        }

        public override string PartPrompt(int part, int player)
        {
            if (Solved || pulled == null || part >= books.Length || pulled[part]) return null;
            return "[E] Pull the " + colourNames[part] + " book";
        }

        protected override void OnPress(int part, int player)
        {
            if (part < 0 || part >= books.Length || pulled[part]) return;
            if (order != null && progress < order.Length && order[progress] == part)
            {
                pulled[part] = true; progress++;
                SfxBank.At(SfxBank.Instance != null ? SfxBank.Instance.itemPickup : null, books[part].position, 0.7f, 0.6f, 10f);
                if (progress >= order.Length) Solve(player);
                return;
            }
            // wrong: everything slams back
            for (int i = 0; i < pulled.Length; i++) pulled[i] = false;
            progress = 0;
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorClose, books[part].position, 1f, 0.6f, 30f);
            Noise(28f);
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The books thump back into the shelf. Wrong book.", 2.5f);
        }

        protected override void OnSolved(int player)
        {
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.wallGrind, bookcase != null ? bookcase.position : transform.position, 1f, 0.8f, 30f);
        }

        protected override void Update()
        {
            base.Update();
            if (books == null || rest == null) return;
            for (int i = 0; i < books.Length; i++)
            {
                Vector3 want = rest[i] + (pulled[i] || Solved ? Vector3.forward * pullDistance : Vector3.zero);
                books[i].localPosition = Vector3.MoveTowards(books[i].localPosition, want, Time.deltaTime * 0.8f);
            }
            if (Solved && bookcase != null && caseT < 1f)
            {
                caseT = Mathf.MoveTowards(caseT, 1f, Time.deltaTime / 2.5f);
                bookcase.localRotation = caseClosed * Quaternion.Euler(0f, swing * Mathf.SmoothStep(0f, 1f, caseT), 0f);
            }
        }

        public override string Describe()
        {
            if (order == null) return title + " (not rolled)";
            var s = title + ": ";
            foreach (var o in order) s += colourNames[o] + " ";
            return s + "(" + progress + "/" + order.Length + ")";
        }
    }
}
