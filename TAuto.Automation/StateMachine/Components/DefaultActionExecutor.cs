using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using TAuto.Core.Models;

namespace TAuto.Automation.StateMachine.Components;

/// <summary>
/// Action executor with mandatory humanization pipeline.
/// Every action passes through: pre-delay → execute → post-variance → hesitation check → risk check.
/// </summary>
public class DefaultActionExecutor : IActionExecutor
{
    private static readonly Random _rng = new();

    public async Task<ActionResult> ExecuteActionsAsync(IEnumerable<IAction> actions, ScriptContext context, string stateName, bool ignoreJump, CancellationToken ct)
    {
        double speedMul = context.Persona?.SpeedMultiplier ?? 1.0;

        foreach (var action in actions)
        {
            if (ct.IsCancellationRequested) break;

            // ── 1. Pre-action Gaussian delay (mandatory, if humanization enabled) ──
            if (context.EnableHumanization)
            {
                int preDelayMs = Math.Max(5, (int)GaussianSample(30 * speedMul, 10 * speedMul));
                try { await Task.Delay(preDelayMs, ct); } catch (OperationCanceledException) { break; }
            }

            // ── 2. Execute the action ──
            var actionResult = await action.ExecuteAsync(context, ct);

            // ── 3. Record to episodic memory ──
            context.Memory.Record(stateName, action.DisplayName, actionResult.Success);

            if (!actionResult.Success && !action.ContinueOnError)
            {
                return ActionResult.Fail($"Action '{action.DisplayName}' failed in state '{stateName}': {actionResult.Message}");
            }

            // ── 4. Post-action micro-variance ──
            if (context.EnableHumanization)
            {
                int postDelayMs = Math.Max(0, (int)GaussianSample(15 * speedMul, 8 * speedMul));
                if (postDelayMs > 0)
                {
                    try { await Task.Delay(postDelayMs, ct); } catch (OperationCanceledException) { break; }
                }

                // ── 5. Hesitation check (2% chance) ──
                if (_rng.NextDouble() < 0.02)
                {
                    int hesitationMs = (int)GaussianSample(400, 150);
                    if (hesitationMs > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Humanizer] Hesitation pause {hesitationMs}ms in '{stateName}'");
                        try { await Task.Delay(hesitationMs, ct); } catch (OperationCanceledException) { break; }
                    }
                }

                // ── 6. Risk-aware micro-break (if RiskScore > 75 and SLA allows) ──
                if (context.Session != null && context.Session.RiskScore > 75)
                {
                    int slaMax = context.Persona?.SlaMaxDowntimeMinutes ?? 15;
                    if (context.Session.CurrentSessionDowntimeMinutes < slaMax)
                    {
                        int breakMs = (int)GaussianSample(3000, 1000); // 2-5s micro-break
                        breakMs = Math.Max(500, breakMs);
                        context.Session.CurrentSessionDowntimeMinutes += breakMs / 60000.0;
                        System.Diagnostics.Debug.WriteLine($"[Humanizer] Micro-break {breakMs}ms (RiskScore={context.Session.RiskScore})");
                        try { await Task.Delay(breakMs, ct); } catch (OperationCanceledException) { break; }
                    }
                }
            }
        }

        if (ignoreJump && !string.IsNullOrEmpty(context.JumpToId))
        {
            System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in actions of state '{stateName}'. Use Transitions instead.");
            context.JumpToId = null;
        }

        return ActionResult.Ok();
    }

    /// <summary>Box-Muller Gaussian sample (reused from BotPersona).</summary>
    private static double GaussianSample(double mean, double stdDev)
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * z;
    }
}
