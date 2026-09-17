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
//using Penumbra.Api.IpcSubscribers.Legacy;
//using Penumbra.Api.Enums;

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

        //private PenumbraService PenumbraService;
        private ResourceLoader ResourceLoader;
        private Configuration Configuration;
        private ConfigurationWindow ConfigurationWindow;

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
                ResourceLoader = new ResourceLoader();
                foreach (var preset in OffsetPresets)
                {
                    var replacement = Path.Combine(PluginInterface.AssemblyLocation.Directory!.FullName, preset.FileName);
                    if (!File.Exists(replacement) || replacement.Length >= 260)
                        throw new InvalidOperationException($"AoAoEnergy VFX file is missing or its path is too long: {preset.FileName}");
                    ResourceLoader.AddReplace(preset.VirtualPath, replacement);
                }
                ResourceLoader.Enable();
                PluginInterface.UiBuilder.Draw += ConfigurationWindow.Draw;
                PluginInterface.UiBuilder.OpenConfigUi += OpenConfiguration;
                PluginInterface.UiBuilder.OpenMainUi += OpenConfiguration;
                (CreateResultVfxHook ?? throw new InvalidOperationException("AoAoEnergy result VFX hook was not initialized.")).Enable();
                PluginLog.Info("AoAoEnergy CN API 15 test build 1.0.4.0: configurable VFX offsets initialized; in-game VFX behavior remains unverified.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (ConfigurationWindow != null)
                PluginInterface.UiBuilder.Draw -= ConfigurationWindow.Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfiguration;
            PluginInterface.UiBuilder.OpenMainUi -= OpenConfiguration;
            CreateResultVfxHook?.Dispose();
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
            _ = Framework.RunOnFrameworkThread(PreviewOnFrameworkThread);
        }

        private void PreviewOnFrameworkThread()
        {
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || localPlayer.Address == IntPtr.Zero)
            {
                PluginLog.Warning("AoAoEnergy preview skipped because the local player is unavailable.");
                return;
            }

            var character = (Character*)localPlayer.Address;
            PlaySelectedVfx(&character->GameObject, &character->GameObject);
        }

        private void OpenConfiguration() => ConfigurationWindow.IsOpen = true;

        private void SaveConfiguration() => PluginInterface.SavePluginConfig(Configuration);

        private static ulong GetCurrentContentId() => PlayerState.IsLoaded ? PlayerState.ContentId : 0;

        private void PlaySelectedVfx(GameObject* cast, GameObject* target)
        {
            var presetIndex = Configuration.OffsetProfiles.GetOffsetIndex(GetCurrentContentId());
            CreateVfx?.Invoke(OffsetPresets[presetIndex].VirtualPath, cast, target, -1, (char)0, 0, (char)0);
        }
    }
}
