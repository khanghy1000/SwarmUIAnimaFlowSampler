using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;

namespace SwarmExtensions.AnimaFlowSampler;

public class AnimaFlowSampler : Extension
{
    public static T2IParamGroup AnimaParamGroup;

    public static T2IRegisteredParam<string> AnimaCFGModeParam;
    public static T2IRegisteredParam<string> AnimaSolverParam;
    public static T2IRegisteredParam<string> AnimaScheduleParam;
    public static T2IRegisteredParam<double> AnimaFlowShiftParam;

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

        AnimaParamGroup = new T2IParamGroup(
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
                Group: AnimaParamGroup,
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
                Group: AnimaParamGroup,
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
                Group: AnimaParamGroup,
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
                Group: AnimaParamGroup,
                FeatureFlag: "comfyui",
                OrderPriority: 4
            )
        );

        WorkflowGenerator.AddStep(
            g =>
            {
                if (
                    g.UserInput.TryGet(AnimaScheduleParam, out _)
                    && g.CurrentCompatClass() == "anima"
                )
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
                                JObject newNode = new JObject();
                                newNode["class_type"] = "AnimaFlowCorrectiveSampler";

                                JObject newInputs = new JObject();
                                JObject oldInputs = nodeData["inputs"] as JObject;

                                if (oldInputs != null)
                                {
                                    newInputs["model"] = oldInputs["model"];
                                    newInputs["positive"] = oldInputs["positive"];
                                    newInputs["negative"] = oldInputs["negative"];
                                    newInputs["latent_image"] = oldInputs["latent_image"];

                                    newInputs["seed"] = oldInputs["noise_seed"];
                                    newInputs["steps"] = oldInputs["steps"];
                                    newInputs["cfg"] = oldInputs["cfg"];
                                    newInputs["control_after_generate"] = oldInputs[
                                        "control_after_generate"
                                    ];
                                    if (newInputs["control_after_generate"].ToString() != "fixed")
                                    {
                                        newInputs["control_after_generate"] = "randomize";
                                    }

                                    newInputs["denoise"] =
                                        1.0
                                        - (
                                            (double)oldInputs["start_at_step"]
                                            / (double)oldInputs["steps"]
                                        );
                                    newInputs["add_noise"] =
                                        oldInputs["add_noise"].ToString() == "enable"
                                            ? "true"
                                            : "false";

                                    newInputs["cfg_mode"] = g.UserInput.TryGet(
                                        AnimaCFGModeParam,
                                        out string cfgMode
                                    )
                                        ? cfgMode
                                        : "const";
                                    newInputs["flow_solver"] = g.UserInput.TryGet(
                                        AnimaSolverParam,
                                        out string flowSolver
                                    )
                                        ? flowSolver
                                        : "flow_euler";
                                    newInputs["flow_schedule"] = g.UserInput.TryGet(
                                        AnimaScheduleParam,
                                        out string flowSchedule
                                    )
                                        ? flowSchedule
                                        : "flow_diffusers_linear_shift";
                                    newInputs["flow_shift"] = g.UserInput.TryGet(
                                        AnimaFlowShiftParam,
                                        out double flowShift
                                    )
                                        ? flowShift
                                        : 3.0;
                                }

                                newNode["inputs"] = newInputs;
                                node.Value = newNode;
                            }
                        }
                    }
                }
            },
            0.5
        );
    }
}
