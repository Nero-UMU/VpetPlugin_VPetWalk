using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.VPetWalk
{
    public class VPetWalkMain : MainPlugin
    {
        public override string PluginName => "Pet Walk";

        private WalkController _walker;
        private StateController _stater;

        private readonly HashSet<WalkDirection> _pressed = new HashSet<WalkDirection>();
        private WalkDirection _activeDir = WalkDirection.None;

        // 长按逻辑
        private DispatcherTimer _holdTimer;
        private bool _isWalkingByHold;
        private const int HoldMs = 1000;

        public VPetWalkMain(IMainWindow mainwin) : base(mainwin) { }

        public override void LoadPlugin()
        {
            _walker = new WalkController(MW.Main, () => MW.GameSavesData.GameSave.CalMode())
            {
                StepPx = 10,
                IntervalMs = 60,
            };

            _stater = new StateController(MW.Main);

            MW.Main.Dispatcher.BeginInvoke(new Action(() =>
            {
                MW.Main.PreviewKeyDown += OnPreviewKeyDown;
                MW.Main.PreviewKeyUp += OnPreviewKeyUp;

                MW.Main.Focusable = true;
                Keyboard.Focus(MW.Main);

                _holdTimer = new DispatcherTimer(DispatcherPriority.Normal, MW.Main.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(HoldMs)
                };
                _holdTimer.Tick += OnHoldTimerTick;

            }), DispatcherPriority.ApplicationIdle);
        }

        private void OnHoldTimerTick(object sender, EventArgs e)
        {
            _holdTimer.Stop();

            // 检查当前锁定的方向是否仍被按下
            if ((_activeDir == WalkDirection.Down || _activeDir == WalkDirection.Up)
                && _pressed.Contains(_activeDir))
            {
                _isWalkingByHold = true;

                // 如果是按住下键触发的移动，需要先从坐下状态切回普通状态
                if (_activeDir == WalkDirection.Down && _stater.IsRunning)
                {
                    _stater.Stop();
                }

                _walker.Start(_activeDir);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return;
            if (!TryMapKeyToDir(e.Key, out var dir)) return;

            _pressed.Add(dir);

            // 方向锁逻辑
            if (_activeDir != WalkDirection.None) return;
            _activeDir = dir;

            _isWalkingByHold = false;

            if (_activeDir == WalkDirection.Down)
            {
                // 1. 立刻尝试坐下
                _stater.Start();
                // 2. 开启长按判定
                _holdTimer.Stop();
                _holdTimer.Start();
            }
            else if (_activeDir == WalkDirection.Up)
            {
                // 1. 立刻停止坐下状态回到普通
                _stater.Stop();
                // 2. 开启长按判定
                _holdTimer.Stop();
                _holdTimer.Start();
            }
            else
            {
                // 左右方向直接走
                _walker.Start(_activeDir);
            }
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!TryMapKeyToDir(e.Key, out var dir)) return;

            _pressed.Remove(dir);

            if (dir != _activeDir) return;

            // 停止当前方向的逻辑
            _holdTimer.Stop();

            if (_activeDir == WalkDirection.Left || _activeDir == WalkDirection.Right || _isWalkingByHold)
            {
                // 如果当前正在走，则停止移动
                _walker.Stop();
            }
            _activeDir = WalkDirection.None;
            _isWalkingByHold = false;
        }

        private bool TryMapKeyToDir(Key key, out WalkDirection dir)
        {
            dir = WalkDirection.None;
            switch (key)
            {
                case Key.A: case Key.Left: dir = WalkDirection.Left; return true;
                case Key.D: case Key.Right: dir = WalkDirection.Right; return true;
                case Key.W: case Key.Up: dir = WalkDirection.Up; return true;
                case Key.S: case Key.Down: dir = WalkDirection.Down; return true;
                default: return false;
            }
        }
    }
}