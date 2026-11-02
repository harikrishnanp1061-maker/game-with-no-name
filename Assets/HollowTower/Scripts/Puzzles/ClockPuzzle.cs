using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The clock tower (campus): its four faces stopped at 3:05. In the arch passage below, two big handwheels drive the
    /// hands - HOURS and MINUTES (five minutes a turn). Set the tower to the night shift's change-over time (written on
    /// the board in the security control room) and the maintenance box beside the wheels unlatches: the generator's
    /// crank handle is inside. Every turn grinds the old gears loudly.
    /// </summary>
    public class ClockPuzzle : WorldPuzzle
    {
        public Transform[] hourPivots, minutePivots;
        public Transform hourWheel, minuteWheel;
        [Tooltip("The control room's shift board line, filled in from the seed.")]
        public TextMesh shiftBoard;
        public int startHour = 3, startMinute = 5;

        int hour, minute, targetHour, targetMinute;
        Quaternion[] hourRest, minuteRest;
        float hourSpin, minuteSpin;

        protected override void Init()
        {
            base.Init();
            hour = startHour; minute = startMinute;
            hourRest = Rest(hourPivots); minuteRest = Rest(minutePivots);
        }

        static Quaternion[] Rest(Transform[] t)
        {
            var r = new Quaternion[t != null ? t.Length : 0];
            for (int i = 0; i < r.Length; i++) r[i] = t[i].localRotation;
            return r;
        }

        protected override void Setup(System.Random rnd)
        {
            do { targetHour = 1 + rnd.Next(12); targetMinute = 5 * rnd.Next(12); } while (targetHour == startHour && targetMinute == startMinute);
            if (shiftBoard != null) shiftBoard.text = "SHIFT CHANGE  " + targetHour.ToString("00") + ":" + targetMinute.ToString("00");
        }

        public override string PartPrompt(int part, int player)
        {
            if (Solved) return null;
            return part == 0 ? "[E] Turn the HOURS wheel   (tower reads " + hour + ":" + minute.ToString("00") + ")" : "[E] Turn the MINUTES wheel   (tower reads " + hour + ":" + minute.ToString("00") + ")";
        }

        protected override void OnPress(int part, int player)
        {
            if (part == 0) { hour = hour % 12 + 1; hourSpin += 90f; }
            else { minute = (minute + 5) % 60; minuteSpin += 30f; }
            SfxBank.At(Tone(90f, 0.4f), transform.position + Vector3.up, 1f, 1f, 30f);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.wallGrind, transform.position + Vector3.up * 2f, 0.5f, 1.6f, 35f);
            Noise(30f);
            if (hour == targetHour && minute == targetMinute) Solve(player);
        }

        protected override void Update()
        {
            base.Update();
            // the hands: twelve o'clock is the rest pose; clockwise seen from the front (about each pivot's local z)
            float hAng = (hour % 12 + minute / 60f) * 30f, mAng = minute * 6f;
            for (int i = 0; i < hourRest.Length; i++) hourPivots[i].localRotation = Quaternion.Slerp(hourPivots[i].localRotation, hourRest[i] * Quaternion.Euler(0f, 0f, -hAng), Time.deltaTime * 4f);
            for (int i = 0; i < minuteRest.Length; i++) minutePivots[i].localRotation = Quaternion.Slerp(minutePivots[i].localRotation, minuteRest[i] * Quaternion.Euler(0f, 0f, -mAng), Time.deltaTime * 4f);
            if (hourWheel != null) { float s = Mathf.MoveTowards(0f, hourSpin, Time.deltaTime * 200f); hourSpin -= s; hourWheel.Rotate(0f, 0f, s, Space.Self); }
            if (minuteWheel != null) { float s = Mathf.MoveTowards(0f, minuteSpin, Time.deltaTime * 200f); minuteSpin -= s; minuteWheel.Rotate(0f, 0f, s, Space.Self); }
        }

        public override string Describe() { return title + ": target " + targetHour + ":" + targetMinute.ToString("00") + " now " + hour + ":" + minute.ToString("00"); }
    }
}
