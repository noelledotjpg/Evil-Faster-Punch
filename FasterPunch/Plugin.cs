using BepInEx;
using HarmonyLib;
using UnityEngine;

using PluginConfig.API;
using PluginConfig.API.Fields;
using static FasterPunch.ConfigManager;
using System.IO;
using System.Reflection;
using PluginConfig;

namespace FasterPunch
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency(PluginConfiguratorController.PLUGIN_GUID)]
    public class Plugin : BaseUnityPlugin
    {
        public void Start()
        {
            ConfigManager.config = PluginConfigurator.Create(PluginInfo.Name, PluginInfo.GUID);

            ConfigManager.StandardEnabled = new BoolField(ConfigManager.config.rootPanel, "Fast Feedbacker", "field.standardenabled", true, true);
            ConfigManager.PunchDelay = new FloatField(ConfigManager.config.rootPanel, "Feedbacker Punch Delay", "field.punchDelay", 0f, 0f, 1f, true, true);

            ConfigManager.StandardEnabled.onValueChange += (e) =>
            {
                ConfigManager.PunchDelay.interactable = e.value;
            };
            ConfigManager.StandardEnabled.TriggerValueChangeEvent();

            ConfigManager.HeavyEnabled = new BoolField(ConfigManager.config.rootPanel, "Fast Knuckleblaster", "field.heavyenabled", true, true);
            
            ConfigManager.HookEnabled = new BoolField(ConfigManager.config.rootPanel, "Fast Whiplash", "field.hookenabled", true, true);
            ConfigManager.WhipThrowSpeed = new FloatField(ConfigManager.config.rootPanel, "Whiplash Throw Speed", "field.whipthrowspeed", 750f, 250f, 1500f, true, true);
            ConfigManager.WhipPullSpeed = new FloatField(ConfigManager.config.rootPanel, "Whiplash Pulling Speed", "field.whipcarryspeed", 120f, 60f, 540f, true, true);

            ConfigManager.HookEnabled.onValueChange += (e) =>
            {
                ConfigManager.WhipThrowSpeed.interactable = e.value;
                ConfigManager.WhipPullSpeed.interactable = e.value;
            };
            ConfigManager.HookEnabled.TriggerValueChangeEvent();

            ConfigManager.ParryUpDamage = new BoolField(ConfigManager.config.rootPanel, "Parry Adds Damage", "field.parryupdamage", true, true);
            ConfigManager.DamageUpType = new EnumField<IncreaseType>(ConfigManager.config.rootPanel, "Parry Damage Increase Type", "field.uptype", IncreaseType.Additive);
            ConfigManager.ParryDamageAmount = new FloatField(ConfigManager.config.rootPanel, "Parry Damage Amount", "field.parrydamageamount", 1f, 1f, 100f, true, true);

            ConfigManager.ParryUpDamage.onValueChange += (e) =>
            {
                ConfigManager.DamageUpType.interactable = e.value;
                ConfigManager.ParryDamageAmount.interactable = e.value;
            };
            ConfigManager.ParryUpDamage.TriggerValueChangeEvent();

            string workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconFilePath = Path.Combine(workingDirectory, "../icon.png");
            ConfigManager.config.SetIconWithURL("file://" + iconFilePath);

            Harmony harm = new Harmony(PluginInfo.GUID);
            harm.PatchAll(typeof(PatchyMcPatchFace));
        }
    }

    public static class Util
    {
        public static string GetGameDirectory()
        {
            // From https://github.com/wafflethings/Atlas/blob/master/Atlas/Utils.cs
            string path = Application.dataPath;
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                path = Utility.ParentDirectory(path, 2);
            }
            else if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                path = Utility.ParentDirectory(path, 1);
            }

            return path;
        }

        public static string GetPluginDirectory()
        {
            return Paths.PluginPath;
        }
    }

    public static class ConfigManager
    {
        public enum IncreaseType
        {
            Additive,
            Multiplicitive
        }

        public static PluginConfigurator config;

        public static FloatField PunchDelay;
        public static FloatField WhipThrowSpeed;
        public static FloatField WhipPullSpeed;

        public static BoolField StandardEnabled;
        public static BoolField HeavyEnabled;
        public static BoolField HookEnabled;

        public static BoolField ParryUpDamage;
        public static EnumField<IncreaseType> DamageUpType;
        public static FloatField ParryDamageAmount;
    }

    public class PatchyMcPatchFace
    {
        public static bool tp = false;
        public static float tpDist = 5f;

        public static float punchWait = 0;
        public static bool punchReady = true;
        [HarmonyPatch(typeof(Punch), nameof(Punch.Update))]
        [HarmonyPrefix]
        public static void SpeedPunch(Punch __instance)
        {
            if (!__instance.shopping)
            {
                if (__instance.type == FistType.Standard && ConfigManager.StandardEnabled.value)
                {
                    if (punchWait > ConfigManager.PunchDelay.value)
                    {
                        punchReady = true;
                    }
                    else
                    {
                        punchWait += Time.deltaTime;
                    }
                    if (__instance.type == FistType.Standard && MonoSingleton<InputManager>.Instance.InputSource.Punch.IsPressed)
                    {
                        if (punchReady)
                        {
                            __instance.PunchStart();
                            punchWait -= ConfigManager.PunchDelay.value;
                            punchReady = false;
                        }
                    }
                    __instance.ReadyToPunch();
                    __instance.cooldownCost = 0f;
                }

                if (__instance.type == FistType.Heavy && ConfigManager.HeavyEnabled.value)
                {
                    __instance.ReadyToPunch();
                    __instance.cooldownCost = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Punch), nameof(Punch.ParryProjectile))]
        [HarmonyPostfix]
        public static void ParryAddDamage(Punch __instance, Projectile proj)
        {
            if (ConfigManager.ParryUpDamage.value)
            {
                Debug.Log("HAHA: " + proj.damage);
                if (ConfigManager.DamageUpType.value == IncreaseType.Additive)
                {
                    proj.damage += ConfigManager.ParryDamageAmount.value;
                } else
                {
                    proj.damage *= ConfigManager.ParryDamageAmount.value;
                }
            }
        }

        /*[HarmonyPatch(typeof(Punch), nameof(Punch.ActiveStart))]
        [HarmonyPrefix]
        public static void ParryAddCannonDamage(Punch __instance)
        {
            if (__instance.type == FistType.Standard)
            {
                Debug.Log("fefee");
            }
        }*/

        [HarmonyPatch(typeof(HookArm), nameof(HookArm.Update))]
        [HarmonyPrefix]
        public static void SpeedHook(HookArm __instance)
        {
            if (ConfigManager.HookEnabled.value)
            {
                __instance.cooldown = 0f;
                __instance.throwWarp = 0f;
            }
        }

        [HarmonyPatch(typeof(HookArm), nameof(HookArm.FixedUpdate))]
        [HarmonyPrefix]
        public static void SpeedHook_FixedUpdate_Prefix(HookArm __instance)
        {
            if (__instance.state == HookState.Ready && __instance.returning)
            {
                __instance.hookPoint = __instance.hand.position;
            }
        }

        [HarmonyPatch(typeof(HookArm), nameof(HookArm.FixedUpdate))]
        [HarmonyPostfix]
        public static void SpeedHook_FixedUpdate_Postfix(HookArm __instance)
        {
            if (ConfigManager.HookEnabled.value)
            {
                if (__instance.state == HookState.Throwing)
                {
                    //very hacky
                    __instance.hookPoint += __instance.throwDirection * (ConfigManager.WhipThrowSpeed.value - 250f) * Time.fixedDeltaTime;
                }

                if (__instance.state == HookState.Pulling)
                {
                    if (__instance.lightTarget)
                    {
                        if (__instance.enemyGroundCheck != null)
                        {
                            if (tp && (MonoSingleton<NewMovement>.Instance.transform.position - __instance.hookPoint).magnitude > (tpDist + 2f))
                            {
                                __instance.enemyRigidbody.position = MonoSingleton<NewMovement>.Instance.transform.position - (MonoSingleton<NewMovement>.Instance.transform.position - __instance.hookPoint).normalized * (tpDist + 2.5f);
                                tp = false;
                            }
                            __instance.enemyRigidbody.velocity = (MonoSingleton<NewMovement>.Instance.transform.position - __instance.hookPoint).normalized * (ConfigManager.WhipPullSpeed.value);
                            return;
                        }
                        if (tp && (MonoSingleton<CameraController>.Instance.transform.position - __instance.hookPoint).magnitude > (tpDist + 2f))
                        {
                            __instance.enemyRigidbody.position = MonoSingleton<CameraController>.Instance.transform.position - (MonoSingleton<CameraController>.Instance.transform.position - __instance.hookPoint).normalized * (tpDist + 2.5f);
                            tp = false;
                        }
                        __instance.enemyRigidbody.velocity = (MonoSingleton<CameraController>.Instance.transform.position - __instance.hookPoint).normalized * (ConfigManager.WhipPullSpeed.value);
                        return;
                    }
                    else
                    {
                        if (!MonoSingleton<NewMovement>.Instance.boost || MonoSingleton<NewMovement>.Instance.sliding)
                        {
                            if (tp && (__instance.hookPoint - MonoSingleton<NewMovement>.Instance.transform.position).magnitude > tpDist)
                            {
                                MonoSingleton<NewMovement>.Instance.rb.position = __instance.hookPoint - (__instance.hookPoint - MonoSingleton<NewMovement>.Instance.transform.position).normalized * tpDist;
                                tp = false;
                            }
                            __instance.caughtEid = null;
                            if (!MonoSingleton<NewMovement>.Instance.boost || MonoSingleton<NewMovement>.Instance.sliding)
                            {
                                MonoSingleton<NewMovement>.Instance.rb.velocity = (__instance.hookPoint - MonoSingleton<NewMovement>.Instance.transform.position).normalized * (ConfigManager.WhipPullSpeed.value);
                                return;
                            }
                            return;
                        }
                    }
                }
                else
                {
                    tp = true;
                }
            }
        }
    }
}
