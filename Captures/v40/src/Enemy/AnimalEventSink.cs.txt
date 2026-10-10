using UnityEngine;

namespace Proto.Enemy
{
    /// <summary>
    /// 동물 클립에 박혀 있는 AnimationEvent를 받아 버린다.
    ///
    /// 팩의 클립들은 발소리·울음소리를 재생하려고 이벤트를 걸어 두었는데,
    /// 그 이벤트를 받던 팩 스크립트를 우리가 꺼 버렸다.
    /// 받는 쪽이 없으면 Unity가 프레임마다 경고를 쏟아내므로 빈 함수로 받아 둔다.
    ///
    /// 나중에 발소리를 넣을 때 여기에 채우면 된다 — 타이밍은 이미 맞춰져 있다.
    /// </summary>
    public class AnimalEventSink : MonoBehaviour
    {
        public void AnimalSound() { }
        public void Attacking() { }
        public void Eating() { }
        public void Running() { }
        public void Walking() { }
    }
}
