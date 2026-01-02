using System;
using System.Windows.Threading;
using VPet_Simulator.Core;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.VPetWalk
{
    public enum WalkDirection { None, Left, Right, Up, Down }

    /// <summary>
    /// 走路控制器
    /// 支持 Left / Right / Up / Down
    /// </summary>
    public class WalkController
    {
        private readonly Main _main;
        private readonly IController _controller;
        private readonly Func<IGameSave.ModeType> _getMood;
        private readonly Dispatcher _ui;

        private DispatcherTimer _moveTimer;

        // 当前状态
        public bool IsWalking { get; private set; }
        public WalkDirection Direction { get; private set; } = WalkDirection.None;

        // 朝向
        private enum FacingLR { Left, Right }
        private FacingLR _facing = FacingLR.Left;

        // 停止时使用
        private WalkDirection _lastDirForAnim = WalkDirection.Left;

        // 参数
        public int StepPx { get; set; } = 10;
        public int IntervalMs { get; set; } = 125;

        private int _token = 0;

        public WalkController(Main main, Func<IGameSave.ModeType> getMood)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _controller = main.Core?.Controller ?? throw new ArgumentNullException("main.Core.Controller");
            _getMood = getMood ?? throw new ArgumentNullException(nameof(getMood));
            _ui = main.Dispatcher;
        }


        public void Start(WalkDirection dir)
        {
            if (dir == WalkDirection.None) { Stop(); return; }

            _ui.BeginInvoke(new Action(() =>
            {
                if (IsWalking && Direction == dir) return;

                Direction = dir;
                IsWalking = true;
                _lastDirForAnim = dir;

                if (dir == WalkDirection.Left) _facing = FacingLR.Left;
                if (dir == WalkDirection.Right) _facing = FacingLR.Right;

                int myToken = ++_token;

                _main.Display(GraphType.Default, AnimatType.Single, () =>
                {
                    if (myToken != _token) return;
                    StartMoveTimer();
                    PlayStartThenLoop(myToken);
                });
            }), DispatcherPriority.ApplicationIdle);
        }

        public void Stop()
        {
            _ui.BeginInvoke(new Action(() =>
            {
                if (!IsWalking) return;

                IsWalking = false;
                Direction = WalkDirection.None;

                int myToken = ++_token;
                StopMoveTimer();

                string anim = GetAnimByDir(_getMood(), _lastDirForAnim);

                _main.Display(anim, AnimatType.C_End, () =>
                {
                    if (myToken != _token) return;
                    _main.DisplayToNomal();
                });
            }), DispatcherPriority.ApplicationIdle);
        }

        public void RefreshAnim()
        {
            if (!IsWalking) return;
            _ui.BeginInvoke(new Action(() =>
            {
                int myToken = ++_token;
                PlayStartThenLoop(myToken);
            }), DispatcherPriority.ApplicationIdle);
        }


        private void PlayStartThenLoop(int myToken)
        {
            if (!IsWalking) return;

            string anim = GetAnimByDir(_getMood(), Direction);

            _main.Display(anim, AnimatType.A_Start, () =>
            {
                if (myToken != _token || !IsWalking) return;
                LoopB(anim, myToken);
            });
        }

        private void LoopB(string anim, int myToken)
        {
            if (myToken != _token || !IsWalking) return;
            _main.Display(anim, AnimatType.B_Loop, () => LoopB(anim, myToken));
        }


        private void StartMoveTimer()
        {
            if (_moveTimer == null)
            {
                _moveTimer = new DispatcherTimer(DispatcherPriority.Normal, _ui);
                _moveTimer.Tick += (s, e) =>
                {
                    if (!IsWalking) return;

                    int dx = 0, dy = 0;

                    switch (Direction)
                    {
                        case WalkDirection.Left:
                            dx = -GetHorizontalSpeedByMood(_getMood());
                            break;

                        case WalkDirection.Right:
                            dx = GetHorizontalSpeedByMood(_getMood());
                            break;

                        case WalkDirection.Up:
                            dy = -StepPx;
                            break;

                        case WalkDirection.Down:
                            dy = StepPx;
                            break;
                    }

                    _controller.MoveWindows(dx, dy);

                };
            }
            _moveTimer.Interval = TimeSpan.FromMilliseconds(IntervalMs);
            _moveTimer.Start();
        }

        private void StopMoveTimer()
        {
            _moveTimer?.Stop();
        }


        private string GetAnimByDir(IGameSave.ModeType mood, WalkDirection dir)
        {
            // 使用朝向
            string lr = _facing == FacingLR.Right ? "right" : "left";

            if (dir == WalkDirection.Up)
                return $"climb.{lr}";
            if (dir == WalkDirection.Down)
                return $"fall.{lr}";

            // Left / Right：walk + 心情
            string suffix = mood switch
            {
                IGameSave.ModeType.Happy => ".faster",
                IGameSave.ModeType.Nomal => "",
                IGameSave.ModeType.PoorCondition => ".slow",
                IGameSave.ModeType.Ill => ".slow",
                _ => ""
            };

            return $"walk.{lr}{suffix}";
        }

        private int GetHorizontalSpeedByMood(IGameSave.ModeType mood)
        {
            return mood switch
            {
                IGameSave.ModeType.Happy => 15,          // faster
                IGameSave.ModeType.Nomal => 10,          // normal
                IGameSave.ModeType.PoorCondition => 5,   // slow
                IGameSave.ModeType.Ill => 5,             // slow
                _ => 10
            };
        }
    }
}
