using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.StateMachine;
using TAuto.Core;

namespace TAuto.Automation.BehaviorTree;

/// <summary>
/// Fluent API for building Behavior Trees with minimal boilerplate.
/// 
/// Usage:
/// <code>
/// var tree = new BehaviorTreeBuilder()
///     .Selector("Root")
///         .Sequence("Safety")
///             .Condition("UnderAttack?", new IfImageFoundAction { ... })
///             .RunFsm("ShieldFSM", () => BuildShieldFsm())
///         .End()
///         .Sequence("Farming")
///             .Condition("IsIdle?", ctx => true)
///             .RunFsm("FarmingFSM", () => BuildFarmingFsm())
///         .End()
///     .End()
///     .Build();
/// </code>
/// </summary>
public class BehaviorTreeBuilder
{
    private readonly Stack<BtNode> _stack = new();
    private BtNode? _root;

    // ── Composite Nodes ──

    /// <summary>
    /// Begin a Selector node (priority/fallback). Children are tried in order.
    /// The first child to succeed or return Running wins.
    /// </summary>
    public BehaviorTreeBuilder Selector(string name)
    {
        var node = new SelectorNode { Name = name };
        PushComposite(node);
        return this;
    }

    /// <summary>
    /// Begin a Sequence node. All children must succeed in order.
    /// Fails on the first failure.
    /// </summary>
    public BehaviorTreeBuilder Sequence(string name)
    {
        var node = new SequenceNode { Name = name };
        PushComposite(node);
        return this;
    }

    /// <summary>
    /// End the current composite node (Selector or Sequence).
    /// </summary>
    public BehaviorTreeBuilder End()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("End() called without a matching Selector/Sequence.");

        var completed = _stack.Pop();

        if (_stack.Count == 0)
        {
            _root = completed;
        }
        // If stack is not empty, the completed node was already added as a child in PushComposite

        return this;
    }

    // ── Leaf Nodes ──

    /// <summary>
    /// Add a condition check using an IAction.
    /// Returns Success if action succeeds, Failure otherwise.
    /// </summary>
    public BehaviorTreeBuilder Condition(string name, IAction condition, bool invert = false)
    {
        var node = new ConditionNode
        {
            Name = name,
            Condition = condition,
            Invert = invert
        };
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add a condition check using a delegate.
    /// </summary>
    public BehaviorTreeBuilder Condition(string name, Func<ScriptContext, CancellationToken, Task<bool>> check)
    {
        var node = new InlineConditionNode(name, check);
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add a condition check using a synchronous delegate.
    /// </summary>
    public BehaviorTreeBuilder Condition(string name, Func<ScriptContext, bool> check)
    {
        var node = new InlineConditionNode(name, (ctx, ct) => Task.FromResult(check(ctx)));
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add an action leaf that executes an IAction.
    /// </summary>
    public BehaviorTreeBuilder Do(string name, IAction action)
    {
        var node = new ActionLeafNode
        {
            Name = name,
            Action = action
        };
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add an action leaf using a delegate.
    /// </summary>
    public BehaviorTreeBuilder Do(string name, Func<ScriptContext, CancellationToken, Task<NodeStatus>> action)
    {
        var node = new InlineActionNode(name, action);
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add an FSM node that wraps a StateMachine.
    /// The factory is called each time the FSM needs to start (supports re-execution).
    /// Returns Running while the FSM is executing.
    /// </summary>
    public BehaviorTreeBuilder RunFsm(string name, Func<StateMachine.StateMachine> fsmFactory)
    {
        var node = new FsmNode
        {
            Name = name,
            FsmFactory = fsmFactory
        };
        AddChild(node);
        return this;
    }

    /// <summary>
    /// Add a pre-built BtNode directly to the tree.
    /// </summary>
    public BehaviorTreeBuilder AddNode(BtNode node)
    {
        AddChild(node);
        return this;
    }

    // ── Build ──

    /// <summary>
    /// Build and return the root node of the behavior tree.
    /// </summary>
    public BtNode Build()
    {
        if (_stack.Count > 0)
            throw new InvalidOperationException(
                $"Unclosed composite nodes: {_stack.Count}. Did you forget to call End()?");

        if (_root == null)
            throw new InvalidOperationException("No root node defined. Start with Selector() or Sequence().");

        return _root;
    }

    // ── Internals ──

    private void PushComposite(BtNode node)
    {
        if (_stack.Count > 0)
        {
            // Add as child of the current composite
            AddChild(node);
        }
        _stack.Push(node);
    }

    private void AddChild(BtNode node)
    {
        if (_stack.Count == 0)
        {
            // If no composite on stack, this becomes the root (single-node tree)
            _root = node;
            return;
        }

        var parent = _stack.Peek();
        if (parent is SelectorNode selector)
        {
            selector.Children.Add(node);
        }
        else if (parent is SequenceNode sequence)
        {
            sequence.Children.Add(node);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot add child to node '{parent.Name}' of type {parent.GetType().Name}. " +
                "Only Selector and Sequence nodes accept children.");
        }
    }
}
