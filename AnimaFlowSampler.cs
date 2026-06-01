using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;

namespace SwarmExtensions.AnimaFlowSampler;

public class AnimaFlowSampler : Extension
{
    public static T2IParamGroup AnimaFlowSamplerMainParamGroup;
    public static T2IRegisteredParam<string> AnimaCFGModeParam;
    public static T2IRegisteredParam<string> AnimaSolverParam;
    public static T2IRegisteredParam<string> AnimaScheduleParam;
    public static T2IRegisteredParam<double> AnimaFlowShiftParam;

    public static T2IParamGroup AnimaFlowSamplerSettingsParamGroup;
    public static T2IRegisteredParam<int> AnimaSettingsFlowErOrderParam;
    public static T2IRegisteredParam<double> AnimaSettingsFlowPc3GammaParam;
    public static T2IRegisteredParam<double> AnimaSettingsFlowPc3ToleranceParam;
    public static T2IRegisteredParam<int> AnimaSettingsFlowUnipcOrderParam;
    public static T2IRegisteredParam<string> AnimaSettingsFlowUnipcSolverTypeParam;
    public static T2IRegisteredParam<bool> AnimaSettingsFlowUnipcLowerOrderFinalParam;
    public static T2IRegisteredParam<int> AnimaSettingsFlowUnipcDisableCorrectorFirstParam;
    public static T2IRegisteredParam<bool> AnimaSettingsFlowUnipcThresholdingParam;
    public static T2IRegisteredParam<double> AnimaSettingsFlowUnipcDynamicThresholdingRatioParam;
    public static T2IRegisteredParam<double> AnimaSettingsFlowUnipcSampleMaxValueParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgEarlyScaleParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgEarlyRampEndParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgPeakBoostParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgBumpStartParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgBumpEndParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgBetaAlphaParam;
    public static T2IRegisteredParam<double> AnimaSettingsCfgBetaBetaParam;
    public static T2IRegisteredParam<double> AnimaSettingsLateCfgScaleParam;
    public static T2IRegisteredParam<double> AnimaSettingsLateCfgStartParam;
    public static T2IRegisteredParam<bool> AnimaSettingsCfgLegacyProgressParam;
    public static T2IRegisteredParam<bool> AnimaSettingsDenoiseLegacyProgressParam;
    public static T2IRegisteredParam<bool> AnimaSettingsFlowRho7TailAutoParam;
    public static T2IRegisteredParam<bool> AnimaSettingsFinalCleanPassParam;
    public static T2IRegisteredParam<double> AnimaSettingsCosmosSigmaMaxParam;
    public static T2IRegisteredParam<double> AnimaSettingsCosmosSigmaMinParam;
    public static T2IRegisteredParam<bool> AnimaSettingsRfEndpointNoiseRefreshEnabledParam;
    public static T2IRegisteredParam<double> AnimaSettingsRfEndpointNoiseRefreshStrengthParam;
    public static T2IRegisteredParam<double> AnimaSettingsRfEndpointNoiseRefreshUntilParam;

    public override void OnInit()
    {
        InstallableFeatures.RegisterInstallableFeature(
            new(
                "anima-sampler",
                "anima-sampler",
                "https://github.com/KeithZ117/Comfyui-anima-sampler",
                "KeithZ117",
                AutoInstall: true
            )
        );

        RegisterMainParams();
        RegisterSettingParams();

        WorkflowGenerator.AddStep(
            g =>
            {
                if (g.UserInput.TryGet(AnimaScheduleParam, out _) && g.IsAnima())
                {
                    foreach (JProperty node in g.Workflow.Properties().ToList())
                    {
                        if (node.Value is JObject nodeData)
                        {
                            JToken classTypeToken = nodeData["class_type"];

                            if (
                                classTypeToken != null
                                && classTypeToken.ToString() == "SwarmKSampler"
                            )
                            {
                                JObject animaSamplerNode = new JObject();
                                animaSamplerNode["class_type"] = "AnimaFlowCorrectiveSampler";

                                JObject animaSamplerInputs = new JObject();
                                JObject kSamplerInputs = nodeData["inputs"] as JObject;

                                if (kSamplerInputs != null)
                                {
                                    animaSamplerInputs = GetMainParamInputs(kSamplerInputs, g);
                                }

                                bool isSettingsEnabled =
                                    g.UserInput.InternalSet.ValuesInput.Keys.Any(key =>
                                        T2IParamTypes.Types.TryGetValue(key, out T2IParamType type)
                                        && type.Group == AnimaFlowSamplerSettingsParamGroup
                                    );
                                if (isSettingsEnabled)
                                {
                                    JObject settingsInputs = GetSettingParamInputs(g);
                                    string settingsNode = g.CreateNode(
                                        "AnimaFlowSettings",
                                        settingsInputs
                                    );
                                    animaSamplerInputs["flow_settings"] = new JArray()
                                    {
                                        settingsNode,
                                        0,
                                    };
                                }

                                animaSamplerNode["inputs"] = animaSamplerInputs;
                                node.Value = animaSamplerNode;
                            }
                        }
                    }
                }
            },
            0.5
        );
    }

    private void RegisterMainParams()
    {
        AnimaFlowSamplerMainParamGroup = new T2IParamGroup(
            "Anima Flow Sampler",
            Toggles: true,
            Open: false,
            IsAdvanced: false,
            OrderPriority: -7
        );

        AnimaCFGModeParam = T2IParamTypes.Register<string>(
            new(
                "Anima CFG Mode",
                "CFG Mode.\n"
                    + "- const: default\n"
                    + "- ramp cfg: starts guidance low and smoothly raises it to the selected cfg",
                "const",
                GetValues: _ => new List<string> { "const", "bump cfg", "ramp cfg" },
                Group: AnimaFlowSamplerMainParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 1
            )
        );

        AnimaSolverParam = T2IParamTypes.Register<string>(
            new(
                "Anima Flow Solver",
                "Solver algorithm used during denoising.",
                "flow_euler",
                GetValues: _ => new List<string>
                {
                    "flow_euler",
                    "flow_ab2",
                    "flow_heun",
                    "flow_pc3_damped",
                    "flow_pc3_diffusers_damped",
                    "flow_3m_damped",
                    "flow_unipc2_x0",
                    "flow_unipc2_diffusers_x0",
                    "flow_er",
                },
                Group: AnimaFlowSamplerMainParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 2
            )
        );

        AnimaScheduleParam = T2IParamTypes.Register<string>(
            new(
                "Anima Flow Schedule",
                "Noise/sigma schedule.",
                "flow_diffusers_linear_shift",
                GetValues: _ => new List<string>
                {
                    "flow_diffusers_linear_shift",
                    "flow_cosmos",
                    "flow_cosmos_rf_tail",
                    "flow_cosmos_lambda_biased_strong",
                    "flow_cosmos_rho7",
                    "flow_rf_linear_shift",
                    "flow_rf_linear_s_tail_shift5",
                    "simple",
                },
                Group: AnimaFlowSamplerMainParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 3
            )
        );

        AnimaFlowShiftParam = T2IParamTypes.Register<double>(
            new(
                "Anima Flow Shift",
                "Default 3.0 and used by shift-aware schedules such as flow_diffusers_linear_shift, flow_cosmos_rf_tail, and flow_rf_linear_shift.",
                "3.0",
                Min: 0.1,
                Max: 20,
                Step: 0.1,
                Group: AnimaFlowSamplerMainParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 4
            )
        );
    }

    private void RegisterSettingParams()
    {
        AnimaFlowSamplerSettingsParamGroup = new T2IParamGroup(
            "Anima Flow Settings",
            Toggles: true,
            Open: false,
            IsAdvanced: true,
            Parent: AnimaFlowSamplerMainParamGroup,
            OrderPriority: 5
        );
        AnimaSettingsFlowErOrderParam = T2IParamTypes.Register<int>(
            new(
                "Anima Flow ER Order",
                "",
                "2",
                Min: 1,
                Max: 3,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 1
            )
        );
        AnimaSettingsFlowPc3GammaParam = T2IParamTypes.Register<double>(
            new(
                "Anima Flow PC3 Gamma",
                "",
                "1.0",
                Min: 0.0,
                Max: 1.0,
                Step: 0.05,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 2
            )
        );
        AnimaSettingsFlowPc3ToleranceParam = T2IParamTypes.Register<double>(
            new(
                "Anima Flow PC3 Tolerance",
                "",
                "0.005",
                Min: 0.0001,
                Max: 0.05,
                Step: 0.0005,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 3
            )
        );
        AnimaSettingsFlowUnipcOrderParam = T2IParamTypes.Register<int>(
            new(
                "Anima Flow UniPC Order",
                "",
                "2",
                Min: 1,
                Max: 6,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 4
            )
        );
        AnimaSettingsFlowUnipcSolverTypeParam = T2IParamTypes.Register<string>(
            new(
                "Anima Flow UniPC Solver Type",
                "",
                "bh2",
                GetValues: _ => new List<string> { "bh1", "bh2" },
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 5
            )
        );
        AnimaSettingsFlowUnipcLowerOrderFinalParam = T2IParamTypes.Register<bool>(
            new(
                "Anima Flow UniPC Lower Order Final",
                "",
                "true",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 6
            )
        );
        AnimaSettingsFlowUnipcDisableCorrectorFirstParam = T2IParamTypes.Register<int>(
            new(
                "Anima Flow UniPC Disable Corrector First",
                "",
                "0",
                Min: 0,
                Max: 10,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 7
            )
        );
        AnimaSettingsFlowUnipcThresholdingParam = T2IParamTypes.Register<bool>(
            new(
                "Anima Flow UniPC Thresholding",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 8
            )
        );
        AnimaSettingsFlowUnipcDynamicThresholdingRatioParam = T2IParamTypes.Register<double>(
            new(
                "Anima Flow UniPC Dynamic Thresholding Ratio",
                "",
                "0.995",
                Min: 0.5,
                Max: 1.0,
                Step: 0.001,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 9
            )
        );
        AnimaSettingsFlowUnipcSampleMaxValueParam = T2IParamTypes.Register<double>(
            new(
                "Anima Flow UniPC Sample Max Value",
                "",
                "1.0",
                Min: 1.0,
                Max: 10.0,
                Step: 0.1,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 10
            )
        );
        AnimaSettingsCfgEarlyScaleParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Early Scale",
                "",
                "1.0",
                Min: 0.0,
                Max: 2.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 11
            )
        );
        AnimaSettingsCfgEarlyRampEndParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Early Ramp End",
                "",
                "0.0",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 12
            )
        );
        AnimaSettingsCfgPeakBoostParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Peak Boost",
                "",
                "0.6",
                Min: 0.0,
                Max: 5.0,
                Step: 0.05,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 13
            )
        );
        AnimaSettingsCfgBumpStartParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Bump Start",
                "",
                "0.0",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 14
            )
        );
        AnimaSettingsCfgBumpEndParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Bump End",
                "",
                "0.27",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 15
            )
        );
        AnimaSettingsCfgBetaAlphaParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Beta Alpha",
                "",
                "2.0",
                Min: 1.0001,
                Max: 20.0,
                Step: 0.1,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 16
            )
        );
        AnimaSettingsCfgBetaBetaParam = T2IParamTypes.Register<double>(
            new(
                "Anima CFG Beta Beta",
                "",
                "3.0",
                Min: 1.0001,
                Max: 20.0,
                Step: 0.1,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 17
            )
        );
        AnimaSettingsLateCfgScaleParam = T2IParamTypes.Register<double>(
            new(
                "Anima Late CFG Scale",
                "",
                "1.0",
                Min: 0.0,
                Max: 2.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 18
            )
        );
        AnimaSettingsLateCfgStartParam = T2IParamTypes.Register<double>(
            new(
                "Anima Late CFG Start",
                "",
                "0.76",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 19
            )
        );
        AnimaSettingsCfgLegacyProgressParam = T2IParamTypes.Register<bool>(
            new(
                "Anima CFG Legacy Progress",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 20
            )
        );
        AnimaSettingsDenoiseLegacyProgressParam = T2IParamTypes.Register<bool>(
            new(
                "Anima Denoise Legacy Progress",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 21
            )
        );
        AnimaSettingsFlowRho7TailAutoParam = T2IParamTypes.Register<bool>(
            new(
                "Anima Flow Rho7 Tail Auto",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 22
            )
        );
        AnimaSettingsFinalCleanPassParam = T2IParamTypes.Register<bool>(
            new(
                "Anima Final Clean Pass",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 23
            )
        );
        AnimaSettingsCosmosSigmaMaxParam = T2IParamTypes.Register<double>(
            new(
                "Anima Cosmos Sigma Max",
                "",
                "80.0",
                Min: 1.0,
                Max: 1000.0,
                Step: 0.5,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 24
            )
        );
        AnimaSettingsCosmosSigmaMinParam = T2IParamTypes.Register<double>(
            new(
                "Anima Cosmos Sigma Min",
                "",
                "0.002",
                Min: 0.0001,
                Max: 1.0,
                Step: 0.0001,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 25
            )
        );
        AnimaSettingsRfEndpointNoiseRefreshEnabledParam = T2IParamTypes.Register<bool>(
            new(
                "Anima RF Endpoint Noise Refresh Enabled",
                "",
                "false",
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 26
            )
        );
        AnimaSettingsRfEndpointNoiseRefreshStrengthParam = T2IParamTypes.Register<double>(
            new(
                "Anima RF Endpoint Noise Refresh Strength",
                "",
                "0.15",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 27
            )
        );
        AnimaSettingsRfEndpointNoiseRefreshUntilParam = T2IParamTypes.Register<double>(
            new(
                "Anima RF Endpoint Noise Refresh Until",
                "",
                "0.2",
                Min: 0.0,
                Max: 1.0,
                Step: 0.01,
                Group: AnimaFlowSamplerSettingsParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 28
            )
        );
    }

    private JObject GetMainParamInputs(JObject kSamplerInputs, WorkflowGenerator g)
    {
        JObject newInputs = new JObject();
        newInputs["model"] = kSamplerInputs["model"];
        newInputs["positive"] = kSamplerInputs["positive"];
        newInputs["negative"] = kSamplerInputs["negative"];
        newInputs["latent_image"] = kSamplerInputs["latent_image"];

        newInputs["seed"] = kSamplerInputs["noise_seed"];
        newInputs["steps"] = kSamplerInputs["steps"];
        newInputs["cfg"] = kSamplerInputs["cfg"];
        newInputs["control_after_generate"] = kSamplerInputs["control_after_generate"];
        if (newInputs["control_after_generate"].ToString() != "fixed")
        {
            newInputs["control_after_generate"] = "randomize";
        }

        newInputs["denoise"] =
            1.0 - ((double)kSamplerInputs["start_at_step"] / (double)kSamplerInputs["steps"]);
        newInputs["add_noise"] = kSamplerInputs["add_noise"].ToString() == "enable";

        newInputs["cfg_mode"] = g.UserInput.TryGet(AnimaCFGModeParam, out string cfgMode)
            ? cfgMode
            : "const";
        newInputs["flow_solver"] = g.UserInput.TryGet(AnimaSolverParam, out string flowSolver)
            ? flowSolver
            : "flow_euler";
        newInputs["flow_schedule"] = g.UserInput.TryGet(AnimaScheduleParam, out string flowSchedule)
            ? flowSchedule
            : "flow_diffusers_linear_shift";
        newInputs["flow_shift"] = g.UserInput.TryGet(AnimaFlowShiftParam, out double flowShift)
            ? flowShift
            : 3.0;

        return newInputs;
    }

    private JObject GetSettingParamInputs(WorkflowGenerator g)
    {
        JObject settingsInputs = new JObject();
        settingsInputs["flow_er_order"] = g.UserInput.TryGet(
            AnimaSettingsFlowErOrderParam,
            out int _flow_er_order
        )
            ? _flow_er_order
            : 2;
        settingsInputs["flow_pc3_gamma"] = g.UserInput.TryGet(
            AnimaSettingsFlowPc3GammaParam,
            out double _flow_pc3_gamma
        )
            ? _flow_pc3_gamma
            : 1.0;
        settingsInputs["flow_pc3_tolerance"] = g.UserInput.TryGet(
            AnimaSettingsFlowPc3ToleranceParam,
            out double _flow_pc3_tolerance
        )
            ? _flow_pc3_tolerance
            : 0.005;
        settingsInputs["flow_unipc_order"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcOrderParam,
            out int _flow_unipc_order
        )
            ? _flow_unipc_order
            : 2;
        settingsInputs["flow_unipc_solver_type"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcSolverTypeParam,
            out string _flow_unipc_solver_type
        )
            ? _flow_unipc_solver_type
            : "bh2";
        settingsInputs["flow_unipc_lower_order_final"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcLowerOrderFinalParam,
            out bool _flow_unipc_lower_order_final
        )
            ? _flow_unipc_lower_order_final
            : true;
        settingsInputs["flow_unipc_disable_corrector_first"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcDisableCorrectorFirstParam,
            out int _flow_unipc_disable_corrector_first
        )
            ? _flow_unipc_disable_corrector_first
            : 0;
        settingsInputs["flow_unipc_thresholding"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcThresholdingParam,
            out bool _flow_unipc_thresholding
        )
            ? _flow_unipc_thresholding
            : false;
        settingsInputs["flow_unipc_dynamic_thresholding_ratio"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcDynamicThresholdingRatioParam,
            out double _flow_unipc_dynamic_thresholding_ratio
        )
            ? _flow_unipc_dynamic_thresholding_ratio
            : 0.995;
        settingsInputs["flow_unipc_sample_max_value"] = g.UserInput.TryGet(
            AnimaSettingsFlowUnipcSampleMaxValueParam,
            out double _flow_unipc_sample_max_value
        )
            ? _flow_unipc_sample_max_value
            : 1.0;
        settingsInputs["cfg_early_scale"] = g.UserInput.TryGet(
            AnimaSettingsCfgEarlyScaleParam,
            out double _cfg_early_scale
        )
            ? _cfg_early_scale
            : 1.0;
        settingsInputs["cfg_early_ramp_end"] = g.UserInput.TryGet(
            AnimaSettingsCfgEarlyRampEndParam,
            out double _cfg_early_ramp_end
        )
            ? _cfg_early_ramp_end
            : 0.0;
        settingsInputs["cfg_peak_boost"] = g.UserInput.TryGet(
            AnimaSettingsCfgPeakBoostParam,
            out double _cfg_peak_boost
        )
            ? _cfg_peak_boost
            : 0.60;
        settingsInputs["cfg_bump_start"] = g.UserInput.TryGet(
            AnimaSettingsCfgBumpStartParam,
            out double _cfg_bump_start
        )
            ? _cfg_bump_start
            : 0.0;
        settingsInputs["cfg_bump_end"] = g.UserInput.TryGet(
            AnimaSettingsCfgBumpEndParam,
            out double _cfg_bump_end
        )
            ? _cfg_bump_end
            : 0.27;
        settingsInputs["cfg_beta_alpha"] = g.UserInput.TryGet(
            AnimaSettingsCfgBetaAlphaParam,
            out double _cfg_beta_alpha
        )
            ? _cfg_beta_alpha
            : 2.0;
        settingsInputs["cfg_beta_beta"] = g.UserInput.TryGet(
            AnimaSettingsCfgBetaBetaParam,
            out double _cfg_beta_beta
        )
            ? _cfg_beta_beta
            : 3.0;
        settingsInputs["late_cfg_scale"] = g.UserInput.TryGet(
            AnimaSettingsLateCfgScaleParam,
            out double _late_cfg_scale
        )
            ? _late_cfg_scale
            : 1.0;
        settingsInputs["late_cfg_start"] = g.UserInput.TryGet(
            AnimaSettingsLateCfgStartParam,
            out double _late_cfg_start
        )
            ? _late_cfg_start
            : 0.76;
        settingsInputs["cfg_legacy_progress"] = g.UserInput.TryGet(
            AnimaSettingsCfgLegacyProgressParam,
            out bool _cfg_legacy_progress
        )
            ? _cfg_legacy_progress
            : false;
        settingsInputs["denoise_legacy_progress"] = g.UserInput.TryGet(
            AnimaSettingsDenoiseLegacyProgressParam,
            out bool _denoise_legacy_progress
        )
            ? _denoise_legacy_progress
            : false;
        settingsInputs["flow_rho7_tail_auto"] = g.UserInput.TryGet(
            AnimaSettingsFlowRho7TailAutoParam,
            out bool _flow_rho7_tail_auto
        )
            ? _flow_rho7_tail_auto
            : false;
        settingsInputs["final_clean_pass"] = g.UserInput.TryGet(
            AnimaSettingsFinalCleanPassParam,
            out bool _final_clean_pass
        )
            ? _final_clean_pass
            : false;
        settingsInputs["cosmos_sigma_max"] = g.UserInput.TryGet(
            AnimaSettingsCosmosSigmaMaxParam,
            out double _cosmos_sigma_max
        )
            ? _cosmos_sigma_max
            : 80.0;
        settingsInputs["cosmos_sigma_min"] = g.UserInput.TryGet(
            AnimaSettingsCosmosSigmaMinParam,
            out double _cosmos_sigma_min
        )
            ? _cosmos_sigma_min
            : 0.002;
        settingsInputs["rf_endpoint_noise_refresh_enabled"] = g.UserInput.TryGet(
            AnimaSettingsRfEndpointNoiseRefreshEnabledParam,
            out bool _rf_endpoint_noise_refresh_enabled
        )
            ? _rf_endpoint_noise_refresh_enabled
            : false;
        settingsInputs["rf_endpoint_noise_refresh_strength"] = g.UserInput.TryGet(
            AnimaSettingsRfEndpointNoiseRefreshStrengthParam,
            out double _rf_endpoint_noise_refresh_strength
        )
            ? _rf_endpoint_noise_refresh_strength
            : 0.15;
        settingsInputs["rf_endpoint_noise_refresh_until"] = g.UserInput.TryGet(
            AnimaSettingsRfEndpointNoiseRefreshUntilParam,
            out double _rf_endpoint_noise_refresh_until
        )
            ? _rf_endpoint_noise_refresh_until
            : 0.20;

        return settingsInputs;
    }
}
