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
        private readonly AudioTimeSyncController.InitData _audioTimeSyncControllerInitData;
        private readonly IReadonlyBeatmapData _readonlyBeatmapData;
        private readonly VRControllersInputManager _vrControllersInputManager;

        private readonly Rect _headSpaceRect = new Rect(1, 1, 2, 2);
        private List<(float start, float end)> _breakTimes = new List<(float start, float end)>();

        private const float SkipMinSeconds = 5f;
        private const float SkipUpUntilSeconds = 2f;
        private const float OutroSkipUpUntilSeconds = 1f;
        private const float ShowSkipAfterSeconds = 0f;

        public bool CanSkip => InBreakPhase;
        public bool InBreakPhase => _breakTimes.Any(b => Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time > b.start && Utilities.AudioTimeSyncSource(ref _audioTimeSyncController).time < b.end);
        public bool WantsToSkip => _audioTimeSyncController.state == AudioTimeSyncController.State.Playing && (_vrControllersInputManager.TriggerValue(XRNode.LeftHand) >= .8 || _vrControllersInputManager.TriggerValue(XRNode.RightHand) >= .8 || Input.GetKey(KeyCode.I));

        public SkipDaemon(Config config, SiraLog siraLog, IVRPlatformHelper vrPlatformHelper, ISkipDisplayService skipDisplayService, AudioTimeSyncController audioTimeSyncController, AudioTimeSyncController.InitData audioTimeSyncControllerInitData, IReadonlyBeatmapData readonlyBeatmapData, VRControllersInputManager vrControllersInputManager)
        {
            _config = config;
            _siraLog = siraLog;
            _audioTimeSyncControllerInitData = audioTimeSyncControllerInitData;
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
            float firstObjectTime = _audioTimeSyncControllerInitData.audioClip.length;
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

            // Intro
            if (_config.AllowIntroSkip && firstObjectTime > SkipMinSeconds)
            {
                _breakTimes.Add((0, firstObjectTime - SkipUpUntilSeconds));
            }

            // Outro
            if (_config.AllowOutroSkip && (_audioTimeSyncControllerInitData.audioClip.length - lastObjectTime) >= SkipMinSeconds)
            {
                _breakTimes.Add((lastObjectTime + ShowSkipAfterSeconds, _audioTimeSyncControllerInitData.audioClip.length - OutroSkipUpUntilSeconds));
            }

            // Breaks
            if (_config.AllowBreakSkip && objectTimes.Count > 1)
            {
                for (int i = 0; i < objectTimes.Count - 1; i++)
                {
                    if (objectTimes[i + 1] - objectTimes[i] >= SkipMinSeconds)
                    {
                        _breakTimes.Add((objectTimes[i] + ShowSkipAfterSeconds, objectTimes[i + 1] - SkipUpUntilSeconds));
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
