using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;
namespace AoAoEnergy
{

    public unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "AoAoEnergy";

        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; set; }
        [PluginService]
        internal static ISigScanner Scanner { get; set; }
        [PluginService]
        internal static ICommandManager CommandManager { get; set; }
        [PluginService]
        internal static IClientState ClientState { get; set; }
        [PluginService]
        internal static IObjectTable ObjectTable { get; set; }
        [PluginService]
        internal static IPlayerState PlayerState { get; set; }
        [PluginService]
        internal static IChatGui ChatGui { get; set; }
        [PluginService]
        internal static IDataManager DataManager { get; set; }
        [PluginService]
        internal static IGameInteropProvider GameInteropProvider { get; set; }
        [PluginService]
        internal static IFramework Framework { get; set; }
        [PluginService]
        internal static IPluginLog PluginLog { get; set; }
        [PluginService]
        internal static ISigScanner SigScanner { get; set; }
        //private delegate IntPtr GetStatusDataDelegate(uint index);
        //[Signature("E8 ?? ?? ?? ?? 0F B7 37", DetourName = nameof(GetStatusDataDetour))]
        //private Hook<GetStatusDataDelegate> GetActionDataHook;
        //private static object GetStatusDataDetour()
        //{
        //    throw new NotImplementedException();
        //}

        //private delegate IntPtr GetStatusHitEffectDataDelegate(uint index);
        //[Signature("E8 ?? ?? ?? ?? 48 85 C0 74 5D 48 8B 1F ", DetourName = nameof(GetStatusHitEffectDataDetour))]
        //private Hook<GetStatusHitEffectDataDelegate> GetStatusHitEffectDataHook;
        //private static object GetStatusHitEffectDataDetour()
        //{
        //    throw new NotImplementedException();
        //}

        private readonly record struct OffsetPreset(string Label, string VirtualPath, string FileName);

        private static readonly OffsetPreset[] OffsetPresets =
        {
            new("原始位置（0 m）", "vfx/common/eff/ev_energydrink_01x_30s.avfx", "ev_energydrink_01x_30s.avfx"),
            new("上移 0.25 m", "vfx/common/eff/aoaoenergy_offset_025.avfx", "ev_energydrink_01x_30s_up_025.avfx"),
            new("上移 0.50 m", "vfx/common/eff/aoaoenergy_offset_050.avfx", "ev_energydrink_01x_30s_up_050.avfx"),
            new("上移 0.75 m", "vfx/common/eff/aoaoenergy_offset_075.avfx", "ev_energydrink_01x_30s_up_075.avfx"),
            new("上移 1.00 m", "vfx/common/eff/aoaoenergy_offset_100.avfx", "ev_energydrink_01x_30s_up_100.avfx"),
            new("上移 1.25 m", "vfx/common/eff/aoaoenergy_offset_125.avfx", "ev_energydrink_01x_30s_up_125.avfx"),
        };

        internal static readonly string[] OffsetLabels = OffsetPresets.Select(preset => preset.Label).ToArray();
        private delegate void CreateResultVfxDelegate(ActionEffectHandler* actionHandler, Character* cast, Character* target, uint action, Effect* result);
        [Signature("48 85 D2 0F 84 ?? ?? ?? ?? 53 55 57", DetourName = nameof(CreateResultVfxDetour))]
        private Hook<CreateResultVfxDelegate> CreateResultVfxHook;

        //private delegate void ApplyOneTargetEffectDelegate(IntPtr actionResult, Character* cast, Character* target, uint action, ActionResult* result);
        //[Signature("E8 ?? ?? ?? ?? 48 FF C6 48 83 FE 08", DetourName = nameof(ApplyOneTargetEffectVfxDetour))]
        //private Hook<ApplyOneTargetEffectDelegate> ApplyOneTargetEffectHook;
        private void CreateResultVfxDetour(ActionEffectHandler* actionHandler, Character* cast, Character* target, uint action, Effect* result)
        {
            if (result != null && result->Type == 14 && result->Value == 49 && cast != null && target != null)
            {
                if (CreateVfx != null)
                {
                    //foreach (var item in actionHandler->IncomingEffects)
                    //{
                    //    PluginLog.Info($"{item.ActionId} {item.ActionType} {action}");
                    //}
                    PlaySelectedVfx(&cast->GameObject, &target->GameObject);
                }
            }
            else
            {
                CreateResultVfxHook.Original(actionHandler, cast, target, action, result);
            }

        }

        private delegate IntPtr CreateVfxDelegate(string path, GameObject* cast, GameObject* target, float speed, char a5, ushort a6, char a7);
        private CreateVfxDelegate CreateVfx;

        private delegate IntPtr ActorVfxRemoveDelegate(IntPtr vfx, char a2);
        private Hook<ActorVfxRemoveDelegate> ActorVfxRemoveHook;
        private const char ImmediateRemovalMode = (char)1;

        private ResourceLoader ResourceLoader;
        private Configuration Configuration;
        private ConfigurationWindow ConfigurationWindow;
        private readonly ActiveVfxTracker ActiveVfx = new();
        private readonly object LifecycleGate = new();
        private bool IsDisposed;

        internal bool RememberPerCharacter => Configuration.OffsetProfiles.RememberPerCharacter;
        internal int SelectedOffsetIndex => Configuration.OffsetProfiles.GetOffsetIndex(GetCurrentContentId());

        public Plugin()
        {
            try
            {
                Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                Configuration.OffsetProfiles ??= new OffsetProfileStore();
                ConfigurationWindow = new ConfigurationWindow(this);

                GameInteropProvider.InitializeFromAttributes(this);
                this.CreateVfx = Marshal.GetDelegateForFunctionPointer<CreateVfxDelegate>(SigScanner.ScanText("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8"));
                var removePointerOffset = SigScanner.ScanText("0F 11 48 10 48 8D 05") + 7;
                var removeAddress = Marshal.ReadIntPtr(removePointerOffset + Marshal.ReadInt32(removePointerOffset) + 4);
                ActorVfxRemoveHook = GameInteropProvider.HookFromAddress<ActorVfxRemoveDelegate>(removeAddress, ActorVfxRemoveDetour);
                ResourceLoader = new ResourceLoader();
                foreach (var preset in OffsetPresets)
                {
                    var replacement = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, preset.FileName);
                    if (!File.Exists(replacement) || replacement.Length >= 260)
                        throw new InvalidOperationException($"AoAoEnergy VFX file is missing or its path is too long: {preset.FileName}");
                    ResourceLoader.AddReplace(preset.VirtualPath, replacement);
                }
                ResourceLoader.Enable();
                ActorVfxRemoveHook.Enable();
                PluginInterface.UiBuilder.Draw += ConfigurationWindow.Draw;
                PluginInterface.UiBuilder.OpenConfigUi += OpenConfiguration;
                PluginInterface.UiBuilder.OpenMainUi += OpenConfiguration;
                (CreateResultVfxHook ?? throw new InvalidOperationException("AoAoEnergy result VFX hook was not initialized.")).Enable();
                PluginLog.Info("AoAoEnergy private fix 1.0.4.2 for CN 7.56 / API 15 initialized.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;
                IsDisposed = true;
            }

            if (ConfigurationWindow != null)
                PluginInterface.UiBuilder.Draw -= ConfigurationWindow.Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfiguration;
            PluginInterface.UiBuilder.OpenMainUi -= OpenConfiguration;
            CreateResultVfxHook?.Dispose();
            ActorVfxRemoveHook?.Dispose();
            ResourceLoader?.Dispose();
        }

        internal void SetRememberPerCharacter(bool enabled)
        {
            Configuration.OffsetProfiles.RememberPerCharacter = enabled;
            SaveConfiguration();
        }

        internal void SetSelectedOffsetIndex(int offsetIndex)
        {
            Configuration.OffsetProfiles.SetOffsetIndex(GetCurrentContentId(), offsetIndex);
            SaveConfiguration();
        }

        internal void PreviewSelectedEffect()
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;
            }
            _ = Framework.RunOnFrameworkThread(PreviewOnFrameworkThread);
        }

        internal void ClearActiveEffects()
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;
            }
            _ = Framework.RunOnFrameworkThread(ClearActiveEffectsOnFrameworkThread);
        }

        private void PreviewOnFrameworkThread()
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;

                var localPlayer = ObjectTable.LocalPlayer;
                if (localPlayer == null || localPlayer.Address == IntPtr.Zero)
                {
                    PluginLog.Warning("AoAoEnergy preview skipped because the local player is unavailable.");
                    return;
                }

                var character = (Character*)localPlayer.Address;
                PlaySelectedVfx(&character->GameObject, &character->GameObject);
            }
        }

        private void OpenConfiguration() => ConfigurationWindow.IsOpen = true;

        private IntPtr ActorVfxRemoveDetour(IntPtr vfx, char a2)
        {
            if (!ActiveVfx.ShouldProcessNaturalRemoval(vfx))
                return IntPtr.Zero;
            return ActorVfxRemoveHook.Original(vfx, a2);
        }

        private void ClearActiveEffectsOnFrameworkThread()
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;

                var claimedVfx = ActiveVfx.ClaimAllForRemoval();
                try
                {
                    foreach (var vfx in claimedVfx)
                        ActorVfxRemoveHook.Original(vfx, ImmediateRemovalMode);
                }
                finally
                {
                    foreach (var vfx in claimedVfx)
                        ActiveVfx.CompleteRemoval(vfx);
                }
            }
        }

        private void SaveConfiguration() => PluginInterface.SavePluginConfig(Configuration);

        private static ulong GetCurrentContentId() => PlayerState.IsLoaded ? PlayerState.ContentId : 0;

        private void PlaySelectedVfx(GameObject* cast, GameObject* target)
        {
            lock (LifecycleGate)
            {
                if (IsDisposed)
                    return;

                var presetIndex = Configuration.OffsetProfiles.GetOffsetIndex(GetCurrentContentId());
                var vfx = CreateVfx?.Invoke(OffsetPresets[presetIndex].VirtualPath, cast, target, -1, (char)0, 0, (char)0) ?? IntPtr.Zero;
                ActiveVfx.Track(vfx);
            }
        }
    }
}
