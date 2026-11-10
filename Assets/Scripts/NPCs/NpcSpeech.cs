using Contract.Core;
using TMPro;
using UnityEngine;

namespace Contract.NPCs
{
    public enum SpeechContext
    {
        Greeting,
        Tourist,
        Bumped,
        GuardGreeting,
        GuardWarning,
        Subject
    }

    /// <summary>Speech bubble (Korean line + English subtitle) above an NPC's head.</summary>
    public sealed class NpcSpeech : MonoBehaviour
    {
        private static readonly (string ko, string en)[] Greeting =
        {
            ("안녕하세요!", "Hello!"), ("네? 저요?", "Huh? Me?"), ("죄송해요, 바빠서요.", "Sorry, I'm in a hurry."),
            ("강남역은 저쪽이에요.", "Gangnam Station is that way."), ("오늘 밤 공기 좋네요.", "Nice night out."),
            ("이 근처 맛집 아세요?", "Know any good places to eat around here?")
        };

        private static readonly (string ko, string en)[] Tourist =
        {
            ("사진 좀 찍어 주실래요?", "Could you take a photo for us?"), ("강남역이 어디예요?", "Where is Gangnam Station?"),
            ("와, 진짜 멋있다!", "Wow, this is amazing!")
        };

        private static readonly (string ko, string en)[] Bumped =
        {
            ("앗!", "Whoa!"), ("조심하세요!", "Watch out!"), ("아이고, 깜짝이야!", "Oh, you startled me!")
        };

        private static readonly (string ko, string en)[] GuardGreeting =
        {
            ("좋은 저녁입니다.", "Good evening."), ("용무가 있으신가요?", "Can I help you?")
        };

        private static readonly (string ko, string en)[] GuardWarning =
        {
            ("여기는 관계자 외 출입금지입니다.", "Staff only beyond this point."), ("잠시만요, 어디 가세요?", "Excuse me, where are you going?")
        };

        [SerializeField] private float height = 2.25f;

        private TextMeshPro bubble;
        private float hideAt;
        private string npcId;

        public bool IsSpeaking => bubble != null && bubble.gameObject.activeSelf;
        public string LastLine { get; private set; }

        public void SetSpeakerId(string id) => npcId = id;

        public void Say(SpeechContext context)
        {
            (string ko, string en)[] table = context switch
            {
                SpeechContext.Tourist => Tourist,
                SpeechContext.Bumped => Bumped,
                SpeechContext.GuardGreeting => GuardGreeting,
                SpeechContext.GuardWarning => GuardWarning,
                _ => Greeting
            };

            var line = table[Random.Range(0, table.Length)];
            Say(line.ko, line.en);
        }

        public void Say(string korean, string english, float seconds = 3.5f)
        {
            EnsureBubble();
            LastLine = korean;
            bubble.text = $"<mark=#0B0D12CC padding=\"14,14,6,6\">{korean}\n<size=62%><color=#C9CED8>{english}</color></size></mark>";
            bubble.gameObject.SetActive(true);
            hideAt = Time.time + seconds;
            GameEvents.RaiseNpcSpoke(npcId, korean, english);
        }

        private void EnsureBubble()
        {
            if (bubble != null) return;
            GameObject go = new GameObject("SpeechBubble", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            bubble = go.AddComponent<TextMeshPro>();
            bubble.fontSize = 2.2f;
            bubble.alignment = TextAlignmentOptions.Center;
            bubble.textWrappingMode = TextWrappingModes.NoWrap;
            bubble.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            bubble.fontStyle = FontStyles.Bold;
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            if (bubble == null || !bubble.gameObject.activeSelf) return;
            if (Time.time >= hideAt)
            {
                bubble.gameObject.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam != null) bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position);
        }
    }
}
