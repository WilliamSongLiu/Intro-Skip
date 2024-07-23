using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace IntroSkip
{
    internal class SkipDaemon : IInitializable, ITickable
    {
        private readonly Config _config;
        private readonly SiraLog _siraLog;
        private readonly IVRPlatformHelper _vrPlatformHelper;
        private readonly ISkipDisplayService _skipDisplayService;
        private AudioTimeSyncController _audioTimeSyncController;
        private readonly IReadonlyBeatmapData _readonlyBeatmapData;
        private readonly AudioTimeSyncController.InitData _initData;
        private readonly VRControllersInputManager _vrControllersInputManager;
        private readonly Rect _headSpaceRect = new Rect(1, 1, 2, 2);

        private List<(float start, float end)> _breakTimes = new List<(float start, float end)>();

        public bool CanSkip => InBreakPhase;
        public bool InBreakPhase => _breakTimes.Any(b => Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time > b.start && Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time < b.end);
        public bool WantsToSkip => _audioTimeSyncController.state == AudioTimeSyncController.State.Playing && (_vrControllersInputManager.TriggerValue(XRNode.LeftHand) >= .8 || _vrControllersInputManager.TriggerValue(XRNode.RightHand) >= .8 || Input.GetKey(KeyCode.I));

        public SkipDaemon(Config config, SiraLog siraLog, IVRPlatformHelper vrPlatformHelper, ISkipDisplayService skipDisplayService, AudioTimeSyncController audioTimeSyncController, IReadonlyBeatmapData readonlyBeatmapData, VRControllersInputManager vrControllersInputManager, AudioTimeSyncController.InitData initData)
        {
            _config = config;
            _siraLog = siraLog;
            _initData = initData;
            _vrPlatformHelper = vrPlatformHelper;
            _skipDisplayService = skipDisplayService;
            _readonlyBeatmapData = readonlyBeatmapData;
            _audioTimeSyncController = audioTimeSyncController;
            _vrControllersInputManager = vrControllersInputManager;
        }

        public void Initialize()
        {
            _breakTimes.Clear();

            var beatmapDataItems = _readonlyBeatmapData.allBeatmapDataItems;
            float firstObjectTime = _initData.audioClip.length;
            float lastObjectTime = -1f;

            int objectCount = 0;
            List<float> objectTimes = new List<float>();

            foreach (var item in beatmapDataItems)
            {
                if (item is NoteData note || (item is ObstacleData obstacle && IsObstacleInHeadArea(obstacle)))
                {
                    objectCount++;
                    objectTimes.Add(item.time);
                    if (item.time < firstObjectTime)
                        firstObjectTime = item.time;
                    if (item.time > lastObjectTime)
                        lastObjectTime = item.time;
                }
            }

            if (objectCount == 0)
                return;

            // Intro as a break
            if (_config.AllowIntroSkip && firstObjectTime > 5f)
            {
                _breakTimes.Add((0, firstObjectTime - 2f));
            }

            // Outro as a break
            if (_config.AllowOutroSkip && (_initData.audioClip.length - lastObjectTime) >= 5f)
            {
                _breakTimes.Add((lastObjectTime + 0.5f, _initData.audioClip.length - 1.5f));
            }

            // Breaks in the middle
            if (_config.AllowBreakSkip && objectTimes.Count > 1)
            {
                for (int i = 0; i < objectTimes.Count - 1; i++)
                {
                    if (objectTimes[i + 1] - objectTimes[i] >= 5f)
                    {
                        _breakTimes.Add((objectTimes[i] + 2f, objectTimes[i + 1] - 2f));
                    }
                }
            }

            foreach (var breakTime in _breakTimes)
            {
                _siraLog.Debug($"Break Skip Time: Start {breakTime.start} | End {breakTime.end}");
            }
        }

        public void Tick()
        {
            if (CanSkip)
            {
                if (!_skipDisplayService.Active)
                    _skipDisplayService.Show();

                if (WantsToSkip)
                {
                    _vrPlatformHelper.TriggerHapticPulse(XRNode.LeftHand, 0.1f, 0.2f, 1);
                    _vrPlatformHelper.TriggerHapticPulse(XRNode.RightHand, 0.1f, 0.2f, 1);
                    var breakTime = _breakTimes.First(b => Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time > b.start && Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time < b.end);
                    Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time = breakTime.end;
                }
            }
            else if (_skipDisplayService.Active && !CanSkip)
            {
                _skipDisplayService.Hide();
                return;
            }
        }

        private bool IsObstacleInHeadArea(ObstacleData data)
        {
            var dataRect = new Rect(data.lineIndex, (int)data.lineLayer, data.width, data.height);
            return _headSpaceRect.Overlaps(dataRect);
        }
    }
}
