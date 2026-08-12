using UnityEngine;
using UnityEngine.XR;

namespace RainbowClock
{
    /// <summary>
    /// 手动实现的设置页滚动：读取手柄摇杆（+桌面滚轮）驱动"目标位置"，
    /// 再以指数阻尼缓动跟随——按住摇杆平滑连续滚动，松开逐渐停住，类似阅读文档。
    /// 内容裁剪由 RectMask2D 负责（GPU 级，无每帧 alpha 操作，避免闪烁/震动）。
    /// </summary>
    public class SettingsScroller : MonoBehaviour
    {
        private RectTransform _content;
        private float _scrollable;
        private float _current;
        private float _target;

        private float _stickSpeed = 300f;
        private float _wheelStep = 80f;
        private float _deadZone = 0.08f;

        private static readonly InputDevice[] DeviceCache = new InputDevice[2];

        public void Setup(RectTransform content, float scrollable)
        {
            _content = content;
            _scrollable = Mathf.Max(0f, scrollable);
            _current = 0f;
            _target = 0f;
        }

        private void Update()
        {
            if (_content == null)
            {
                return;
            }

            // 摇杆：推动时持续累加目标
            Vector2 axis = GetStickAxis();
            float stickInput = -axis.y; // 上推向上滚
            if (Mathf.Abs(stickInput) < _deadZone)
            {
                stickInput = 0f;
            }
            _target += stickInput * Time.deltaTime * _stickSpeed;

            // 滚轮：每格跳一段目标
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                _target += wheel * _wheelStep;
            }

            _target = Mathf.Clamp(_target, 0f, _scrollable);

            // 指数阻尼缓动：连续平滑跟随目标
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (Mathf.Abs(_current - _target) < 0.01f)
            {
                _current = _target;
            }

            Vector2 pos = _content.anchoredPosition;
            pos.y = _current;
            _content.anchoredPosition = pos;
        }

        private static Vector2 GetStickAxis()
        {
            Vector2 best = Vector2.zero;
            float bestMag = 0f;
            foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                InputDevice device = GetDevice(node);
                if (!device.isValid)
                {
                    continue;
                }
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
                    && axis.sqrMagnitude > bestMag)
                {
                    bestMag = axis.sqrMagnitude;
                    best = axis;
                }
            }
            return best;
        }

        /// <summary>缓存左右手柄设备引用，失效时重新获取（避免每帧枚举设备）。</summary>
        private static InputDevice GetDevice(XRNode node)
        {
            int index = node == XRNode.LeftHand ? 0 : 1;
            if (!DeviceCache[index].isValid)
            {
                DeviceCache[index] = InputDevices.GetDeviceAtXRNode(node);
            }
            return DeviceCache[index];
        }
    }
}
