using System.Collections;
using UnityEngine;

namespace RainbowClock
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("RainbowClockCoroutineRunner");
                    _instance = obj.AddComponent<CoroutineRunner>();
                    GameObject.DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        public static Coroutine StartRoutine(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        public static void StopRoutine(Coroutine routine)
        {
            if (_instance != null)
            {
                _instance.StopCoroutine(routine);
            }
        }
    }
}
