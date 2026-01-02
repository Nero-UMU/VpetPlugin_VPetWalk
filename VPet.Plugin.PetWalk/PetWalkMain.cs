using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.PetWalk
{
    public class PetWalkMain : MainPlugin
    {
        public override string PluginName => "Pet Walk";

        private WalkController _walker;

        // 当前按下的方向键集合
        private readonly HashSet<WalkDirection> _pressed = new HashSet<WalkDirection>();

        // 当前“锁定/激活”的方向：只要它没松开，就不允许别的方向抢占
        private WalkDirection _activeDir = WalkDirection.None;

        public PetWalkMain(IMainWindow mainwin) : base(mainwin) { }

        public override void LoadPlugin()
        {
            _walker = new WalkController(
                MW.Main,
                () => MW.GameSavesData.GameSave.CalMode()
            )
            {
                StepPx = 10,
                IntervalMs = 60, // 16~80 越小越跟手
            };

            // 等 UI 就绪后再挂事件，避免时序问题
            MW.Main.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 挂键盘事件：只在 VPet 窗口有焦点时触发
                MW.Main.PreviewKeyDown += OnPreviewKeyDown;
                MW.Main.PreviewKeyUp += OnPreviewKeyUp;

                //（可选）确保窗口能接收键盘
                MW.Main.Focusable = true;
                Keyboard.Focus(MW.Main);

            }), DispatcherPriority.ApplicationIdle);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return; // 按住时系统会重复触发 KeyDown，忽略

            if (!TryMapKeyToDir(e.Key, out var dir))
                return;

            // 记录按下
            _pressed.Add(dir);

            // 方向锁：只有在“完全静止/未锁定方向”时，才接受新方向启动
            if (_activeDir == WalkDirection.None)
            {
                _activeDir = dir;
                _walker.Start(_activeDir);
            }
            // 否则：已有方向在走，忽略新的方向键，不改变方向

            e.Handled = false; // 你也可以改 true 来吞掉键盘事件
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!TryMapKeyToDir(e.Key, out var dir))
                return;

            _pressed.Remove(dir);

            // 只有“当前激活方向”松开时，才允许切换/停止
            if (dir != _activeDir)
            {
                e.Handled = false;
                return;
            }

            // 当前方向松开了：若没有其他方向按着 -> 停止；否则从剩余按键里选一个继续
            if (_pressed.Count == 0)
            {
                _activeDir = WalkDirection.None;
                _walker.Stop();
            }
            else
            {
                // 从仍按着的方向里选一个（这里随便取一个；你也可以自定义优先级）
                foreach (var d in _pressed)
                {
                    _activeDir = d;
                    _walker.Start(_activeDir);
                    break;
                }
            }

            e.Handled = false;
        }

        private bool TryMapKeyToDir(Key key, out WalkDirection dir)
        {
            dir = WalkDirection.None;

            switch (key)
            {
                // WASD
                case Key.A: dir = WalkDirection.Left; return true;
                case Key.D: dir = WalkDirection.Right; return true;
                case Key.W: dir = WalkDirection.Up; return true;
                case Key.S: dir = WalkDirection.Down; return true;

                // 方向键
                case Key.Left: dir = WalkDirection.Left; return true;
                case Key.Right: dir = WalkDirection.Right; return true;
                case Key.Up: dir = WalkDirection.Up; return true;
                case Key.Down: dir = WalkDirection.Down; return true;

                default:
                    return false;
            }
        }
    }
}
