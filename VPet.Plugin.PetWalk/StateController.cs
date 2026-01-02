using System;
using System.Windows.Threading;
using VPet_Simulator.Core;
using static VPet_Simulator.Core.GraphInfo;

namespace VPet.Plugin.VPetWalk
{
    /// <summary>
    /// 控制桌宠的坐下/站起状态
    /// </summary>
    public class StateController
    {
        private readonly Main _main;
        private readonly Dispatcher _ui;

        public int looptimes;
        public int CountNomal;

        private bool _running = false;
        private int _token = 0;

        public bool IsRunning => _running;

        // 记住当前坐姿动画信息
        private string _currentGraphName = null;
        private GraphType _currentStateType = GraphType.StateONE;

        public StateController(Main main)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _ui = main.Dispatcher ?? throw new ArgumentNullException(nameof(main.Dispatcher));
        }

        /// <summary>
        /// 启动坐下/站起状态
        /// 如果已经在坐：跳过 A_Start，直接进入当前动画的 B_Loop
        /// </summary>
        public void Start()
        {
            _ui.BeginInvoke(new Action(() =>
            {
                // 已经在坐：直接进 B
                if (_running)
                {
                    JumpToBLoopIfSitting();
                    return;
                }

                _running = true;
                int myToken = ++_token;

                // 清空当前记录，重新开始一套
                _currentGraphName = null;
                _currentStateType = GraphType.StateONE;

                DisplayIdel_StateONE_AStart(myToken);

            }), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// 停止坐下/站起状态
        /// </summary>
        public void Stop()
        {
            _ui.BeginInvoke(new Action(() =>
            {
                if (!_running) return;

                _running = false;

                int stopToken = ++_token;

                string endGraph = _currentGraphName;
                GraphType endType = _currentStateType;

                _currentGraphName = null;

                // 如果不知道当前播的是哪个，就只能直接回普通
                if (string.IsNullOrEmpty(endGraph))
                {
                    _main.DisplayToNomal();
                    return;
                }

                // 播放收尾动画 C_End，然后回普通
                _main.Display(endGraph, AnimatType.C_End, endType, () =>
                {
                    if (stopToken != _token) return;
                    _main.DisplayToNomal();
                });

            }), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// 已经坐着时，再次 Start：跳过 A_Start，直接播一次 B_Loop 并接回原状态机循环
        /// </summary>
        private void JumpToBLoopIfSitting()
        {
            if (string.IsNullOrEmpty(_currentGraphName))
                return;

            int myToken = _token;
            if (!_running || myToken != _token) return;

            looptimes = 0;

            // 直接进 B_Loop
            if (_currentStateType == GraphType.StateTWO)
            {
                _main.Display(
                    _currentGraphName,
                    AnimatType.B_Loop,
                    GraphType.StateTWO,
                    () => DisplayIdel_StateTWOing(_currentGraphName, myToken)
                );
            }
            else
            {
                _main.Display(
                    _currentGraphName,
                    AnimatType.B_Loop,
                    GraphType.StateONE,
                    () => DisplayIdel_StateONEing(_currentGraphName, myToken)
                );
            }
        }

        /// <summary>
        /// StateONE 入口：A_Start
        /// </summary>
        private void DisplayIdel_StateONE_AStart(int myToken)
        {
            if (!_running || myToken != _token) return;

            looptimes = 0;
            CountNomal = 0;

            var name = _main.Core.Graph.FindName(GraphType.StateONE);
            var list = _main.Core.Graph
                .FindGraphs(name, AnimatType.A_Start, _main.Core.Save.Mode)?
                .FindAll(x => x.GraphInfo.Type == GraphType.StateONE);

            if (list != null && list.Count > 0)
            {
                var chosen = list[Function.Rnd.Next(list.Count)];

                // 记录当前坐姿动画
                _currentGraphName = chosen.GraphInfo.Name;
                _currentStateType = GraphType.StateONE;

                _main.Display(
                    chosen,
                    () => DisplayIdel_StateONEing(_currentGraphName, myToken)
                );
            }
            else
            {
                // 若没有列表，则按类型播放
                _currentGraphName = name;
                _currentStateType = GraphType.StateONE;

                _main.Display(
                    GraphType.StateONE,
                    AnimatType.A_Start,
                    () => DisplayIdel_StateONEing(_currentGraphName, myToken)
                );
            }
        }

        /// <summary>
        /// StateONE 循环：B_Loop 或切换/结束
        /// </summary>
        private void DisplayIdel_StateONEing(string graphname, int myToken)
        {
            if (!_running || myToken != _token) return;

            _currentGraphName = graphname;
            _currentStateType = GraphType.StateONE;

            if (Function.Rnd.Next(++looptimes) > _main.Core.Graph.GraphConfig.GetDuration(graphname))
            {
                switch (Function.Rnd.Next(2 + CountNomal))
                {
                    case 0:
                        DisplayIdel_StateTWO_AStart(graphname, myToken);
                        break;

                    default:
                        _main.Display(graphname, AnimatType.C_End, GraphType.StateONE, () =>
                        {
                            _running = false;
                            _currentGraphName = null;
                            _main.DisplayToNomal();
                        });
                        break;
                }
            }
            else
            {
                _main.Display(
                    graphname,
                    AnimatType.B_Loop,
                    GraphType.StateONE,
                    () => DisplayIdel_StateONEing(graphname, myToken)
                );
            }
        }

        /// <summary>
        /// StateTWO：A_Start
        /// </summary>
        private void DisplayIdel_StateTWO_AStart(string graphname, int myToken)
        {
            if (!_running || myToken != _token) return;

            looptimes = 0;
            CountNomal++;

            _currentGraphName = graphname;
            _currentStateType = GraphType.StateTWO;

            _main.Display(
                graphname,
                AnimatType.A_Start,
                GraphType.StateTWO,
                () => DisplayIdel_StateTWOing(graphname, myToken)
            );
        }

        /// <summary>
        /// StateTWO 循环：B_Loop 或结束回 StateONE
        /// </summary>
        private void DisplayIdel_StateTWOing(string graphname, int myToken)
        {
            if (!_running || myToken != _token) return;

            _currentGraphName = graphname;
            _currentStateType = GraphType.StateTWO;

            if (Function.Rnd.Next(++looptimes) > _main.Core.Graph.GraphConfig.GetDuration(graphname))
            {
                looptimes = 0;

                // StateTWO 结束后回到 StateONE 的循环
                _main.Display(
                    graphname,
                    AnimatType.C_End,
                    GraphType.StateTWO,
                    () => DisplayIdel_StateONEing(graphname, myToken)
                );
            }
            else
            {
                _main.Display(
                    graphname,
                    AnimatType.B_Loop,
                    GraphType.StateTWO,
                    () => DisplayIdel_StateTWOing(graphname, myToken)
                );
            }
        }
    }
}
