﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using RetriX.Shared.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RetriX.Shared.ViewModels
{
    public class GamePlayerVM : ViewModelBase
    {
        private static readonly TimeSpan PriodicChecksInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan UIHidingTime = TimeSpan.FromSeconds(4);

        private readonly IPlatformService PlatformService;
        private readonly IEmulationService EmulationService;
        private readonly ISaveStateService SaveStateService;

        public RelayCommand TappedCommand { get; private set; }
        public RelayCommand PointerMovedCommand { get; private set; }
        public RelayCommand ToggleFullScreenCommand { get; private set; }

        public RelayCommand TogglePauseCommand { get; private set; }
        public RelayCommand ResetCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }

        public RelayCommand SaveStateSlot1 { get; private set; }
        public RelayCommand SaveStateSlot2 { get; private set; }
        public RelayCommand SaveStateSlot3 { get; private set; }
        public RelayCommand SaveStateSlot4 { get; private set; }
        public RelayCommand SaveStateSlot5 { get; private set; }
        public RelayCommand SaveStateSlot6 { get; private set; }

        public RelayCommand LoadStateSlot1 { get; private set; }
        public RelayCommand LoadStateSlot2 { get; private set; }
        public RelayCommand LoadStateSlot3 { get; private set; }
        public RelayCommand LoadStateSlot4 { get; private set; }
        public RelayCommand LoadStateSlot5 { get; private set; }
        public RelayCommand LoadStateSlot6 { get; private set; }

        public RelayCommand<InjectedInputTypes> InjectInputCommand { get; set; }

        private RelayCommand[] AllCoreCommands;

        private bool coreOperationsAllowed = false;
        public bool CoreOperationsAllowed
        {
            get { return coreOperationsAllowed; }
            set
            {
                if (Set(ref coreOperationsAllowed, value))
                {
                    foreach (var i in AllCoreCommands)
                    {
                        i.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public bool FullScreenChangingPossible => PlatformService.FullScreenChangingPossible;
        public bool IsFullScreenMode => PlatformService.IsFullScreenMode;

        private bool shouldDisplayTouchGamepad;
        public bool ShouldDisplayTouchGamepad
        {
            get { return shouldDisplayTouchGamepad; }
            private set { Set(ref shouldDisplayTouchGamepad, value); }
        }

        private bool gameIsPaused;
        public bool GameIsPaused
        {
            get { return gameIsPaused; }
            set { Set(ref gameIsPaused, value); }
        }

        private bool displayPlayerUI;
        public bool DisplayPlayerUI
        {
            get { return displayPlayerUI; }
            set
            {
                Set(ref displayPlayerUI, value);
                if(value)
                {
                    PlayerUIDisplayTime = DateTimeOffset.UtcNow;
                }
            }
        }

        private Timer PeriodicChecksTimer;
        private DateTimeOffset PlayerUIDisplayTime = DateTimeOffset.UtcNow;
        private DateTimeOffset LastPointerMoveTime = DateTimeOffset.UtcNow;

        public GamePlayerVM(IPlatformService platformService, IEmulationService emulationService, ISaveStateService saveStateService)
        {
            PlatformService = platformService;
            EmulationService = emulationService;
            SaveStateService = saveStateService;

            ShouldDisplayTouchGamepad = PlatformService.ShouldDisplayTouchGamepad;

            TappedCommand = new RelayCommand(() =>
            {
                DisplayPlayerUI = !DisplayPlayerUI;
            });

            PointerMovedCommand = new RelayCommand(() =>
            {
                PlatformService.ChangeMousePointerVisibility(MousePointerVisibility.Visible);
                LastPointerMoveTime = DateTimeOffset.UtcNow;
                DisplayPlayerUI = true;
            });

            ToggleFullScreenCommand = new RelayCommand(() => RequestFullScreenChange(FullScreenChangeType.Toggle));

            TogglePauseCommand = new RelayCommand(() => { var task = TogglePause(false); }, () => CoreOperationsAllowed);
            ResetCommand = new RelayCommand(Reset, () => CoreOperationsAllowed);
            StopCommand = new RelayCommand(Stop, () => CoreOperationsAllowed);

            SaveStateSlot1 = new RelayCommand(() => SaveState(1), () => CoreOperationsAllowed);
            SaveStateSlot2 = new RelayCommand(() => SaveState(2), () => CoreOperationsAllowed);
            SaveStateSlot3 = new RelayCommand(() => SaveState(3), () => CoreOperationsAllowed);
            SaveStateSlot4 = new RelayCommand(() => SaveState(4), () => CoreOperationsAllowed);
            SaveStateSlot5 = new RelayCommand(() => SaveState(5), () => CoreOperationsAllowed);
            SaveStateSlot6 = new RelayCommand(() => SaveState(6), () => CoreOperationsAllowed);

            LoadStateSlot1 = new RelayCommand(() => LoadState(1), () => CoreOperationsAllowed);
            LoadStateSlot2 = new RelayCommand(() => LoadState(2), () => CoreOperationsAllowed);
            LoadStateSlot3 = new RelayCommand(() => LoadState(3), () => CoreOperationsAllowed);
            LoadStateSlot4 = new RelayCommand(() => LoadState(4), () => CoreOperationsAllowed);
            LoadStateSlot5 = new RelayCommand(() => LoadState(5), () => CoreOperationsAllowed);
            LoadStateSlot6 = new RelayCommand(() => LoadState(6), () => CoreOperationsAllowed);

            InjectInputCommand = new RelayCommand<InjectedInputTypes>(d => EmulationService.InjectInputPlayer1(d));

            AllCoreCommands = new RelayCommand[] { TogglePauseCommand, ResetCommand, StopCommand,
                SaveStateSlot1, SaveStateSlot2, SaveStateSlot3, SaveStateSlot4, SaveStateSlot5, SaveStateSlot6,
                LoadStateSlot1, LoadStateSlot2, LoadStateSlot3, LoadStateSlot4, LoadStateSlot5, LoadStateSlot6
            };

            EmulationService.GameStarted += OnGameStarted;
            PlatformService.FullScreenChangeRequested += (d, e) => RequestFullScreenChange(e.Type);
            PlatformService.PauseToggleRequested += d => OnPauseToggleKey();
            PlatformService.GameStateOperationRequested += OnGameStateOperationRequested;
        }

        private async void RequestFullScreenChange(FullScreenChangeType fullScreenChangeType)
        {
            PlatformService.ChangeFullScreenState(fullScreenChangeType);

            //Fullscreen toggling takes some time
            await Task.Delay(100);
            RaisePropertyChanged(nameof(IsFullScreenMode));
        }

        public void Activated()
        {
            CoreOperationsAllowed = true;
            PlatformService.HandleGameplayKeyShortcuts = true;
            DisplayPlayerUI = true;
            PeriodicChecksTimer = new Timer(d => PeriodicChecks(), null, PriodicChecksInterval, PriodicChecksInterval);
        }

        public void Deactivated()
        {
            PeriodicChecksTimer.Dispose();
            CoreOperationsAllowed = false;
            PlatformService.HandleGameplayKeyShortcuts = false;
            PlatformService.ChangeMousePointerVisibility(MousePointerVisibility.Visible);
        }

        private async Task TogglePause(bool dismissOverlayImmediately)
        {
            if (!CoreOperationsAllowed)
            {
                return;
            }

            CoreOperationsAllowed = false;

            if (GameIsPaused)
            {
                await EmulationService.ResumeGameAsync();
                if (dismissOverlayImmediately)
                {
                    DisplayPlayerUI = false;
                }
            }
            else
            {
                await EmulationService.PauseGameAsync();
                DisplayPlayerUI = true;
            }

            GameIsPaused = !GameIsPaused;
            CoreOperationsAllowed = true;
        }

        private async void OnPauseToggleKey()
        {
            await TogglePause(true);
            if (GameIsPaused)
            {
                PlatformService.ForceUIElementFocus();
            }
        }

        private async void Reset()
        {
            CoreOperationsAllowed = false;
            await EmulationService.ResetGameAsync();
            CoreOperationsAllowed = true;

            if (GameIsPaused)
            {
                await TogglePause(true);
            }
        }

        private async void Stop()
        {
            CoreOperationsAllowed = false;
            await EmulationService.StopGameAsync();
            CoreOperationsAllowed = true;
        }

        private async void SaveState(uint slotID)
        {
            CoreOperationsAllowed = false;

            var data = await EmulationService.SaveGameStateAsync();
            if (data != null)
            {
                SaveStateService.SetGameId(EmulationService.GameID);
                await SaveStateService.SaveStateAsync(slotID, data);
            }

            CoreOperationsAllowed = true;

            if (GameIsPaused)
            {
                await TogglePause(true);
            }
        }

        private async void LoadState(uint slotID)
        {
            CoreOperationsAllowed = false;

            SaveStateService.SetGameId(EmulationService.GameID);
            var data = await SaveStateService.LoadStateAsync(slotID);
            if (data != null)
            {
                await EmulationService.LoadGameStateAsync(data);
            }

            CoreOperationsAllowed = true;

            if (GameIsPaused)
            {
                await TogglePause(true);
            }
        }

        private void OnGameStarted(IEmulationService sender)
        {
            GameIsPaused = false;
        }

        private void OnGameStateOperationRequested(IPlatformService sender, GameStateOperationEventArgs args)
        {
            if (!CoreOperationsAllowed)
            {
                return;
            }

            if (args.Type == GameStateOperationEventArgs.GameStateOperationType.Load)
            {
                LoadState(args.SlotID);
            }
            else
            {
                SaveState(args.SlotID);
            }
        }

        private void PeriodicChecks()
        {
            var displayTouchGamepad = PlatformService.ShouldDisplayTouchGamepad;
            if (ShouldDisplayTouchGamepad != displayTouchGamepad)
            {
                PlatformService.RunOnUIThreadAsync(() => ShouldDisplayTouchGamepad = displayTouchGamepad);
            }

            if (GameIsPaused)
            {
                return;
            }

            var currentTime = DateTimeOffset.UtcNow;

            if (currentTime.Subtract(LastPointerMoveTime).CompareTo(UIHidingTime) >= 0)
            {
                PlatformService.RunOnUIThreadAsync(() => PlatformService.ChangeMousePointerVisibility(MousePointerVisibility.Hidden));
            }

            if (currentTime.Subtract(PlayerUIDisplayTime).CompareTo(UIHidingTime) >= 0)
            {
                PlatformService.RunOnUIThreadAsync(() => DisplayPlayerUI = false);
            }
        }
    }
}
