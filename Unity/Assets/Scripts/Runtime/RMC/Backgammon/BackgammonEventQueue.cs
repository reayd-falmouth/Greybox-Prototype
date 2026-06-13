using System;
using System.Collections.Generic;
using UnityEngine;

public enum BackgammonEventClockDomain
{
    ScaledGameplay = 0,
    UnscaledReal = 1
}

public readonly struct BackgammonPresentationEvent
{
    public readonly string Name;
    public readonly Action Dispatch;
    public readonly bool Blocking;
    public readonly float MinDelaySeconds;
    public readonly BackgammonEventClockDomain ClockDomain;

    public BackgammonPresentationEvent(
        string name,
        Action dispatch,
        bool blocking,
        float minDelaySeconds,
        BackgammonEventClockDomain clockDomain)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
        Dispatch = dispatch;
        Blocking = blocking;
        MinDelaySeconds = Mathf.Max(0f, minDelaySeconds);
        ClockDomain = clockDomain;
    }
}

/// <summary>
/// Lightweight FIFO event queue for presentation side effects.
/// </summary>
public sealed class BackgammonEventQueue
{
    private readonly Queue<BackgammonPresentationEvent> _fifo = new();
    private readonly bool _enableDebugLogs;
    private readonly Action<string> _logger;

    private float _blockingTimer;
    private BackgammonEventClockDomain _blockingClockDomain;
    private float _gameSpeedMultiplier = 1f;

    public int PendingCount => _fifo.Count;

    public BackgammonEventQueue(bool enableDebugLogs, Action<string> logger = null)
    {
        _enableDebugLogs = enableDebugLogs;
        _logger = logger;
    }

    public void SetGameSpeedMultiplier(float multiplier)
    {
        _gameSpeedMultiplier = Mathf.Max(0.01f, multiplier);
    }

    public void Enqueue(in BackgammonPresentationEvent presentationEvent)
    {
        _fifo.Enqueue(presentationEvent);
        if (_enableDebugLogs)
            Log($"[Backgammon][EventQueue] Enqueue name={presentationEvent.Name} blocking={presentationEvent.Blocking} delay={presentationEvent.MinDelaySeconds:F3}s domain={presentationEvent.ClockDomain} pending={_fifo.Count}");
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (_blockingTimer > 0f)
        {
            float consume = ResolveDeltaForDomain(unscaledDeltaTime, _blockingClockDomain);
            _blockingTimer = Mathf.Max(0f, _blockingTimer - consume);
            if (_blockingTimer > 0f)
                return;
        }

        if (_fifo.Count == 0)
            return;

        BackgammonPresentationEvent next = _fifo.Dequeue();
        try
        {
            next.Dispatch?.Invoke();
            if (_enableDebugLogs)
                Log($"[Backgammon][EventQueue] Dispatch name={next.Name} pendingAfter={_fifo.Count}");
        }
        catch (Exception ex)
        {
            Log($"[Backgammon][EventQueue] Dispatch failure name={next.Name} exception={ex.GetType().Name} message={ex.Message}");
        }

        if (!next.Blocking || next.MinDelaySeconds <= 0f)
            return;

        _blockingClockDomain = next.ClockDomain;
        _blockingTimer = next.MinDelaySeconds;
        if (_enableDebugLogs)
            Log($"[Backgammon][EventQueue] Block start name={next.Name} timer={_blockingTimer:F3}s domain={_blockingClockDomain}");
    }

    private float ResolveDeltaForDomain(float unscaledDeltaTime, BackgammonEventClockDomain domain)
    {
        return domain == BackgammonEventClockDomain.ScaledGameplay
            ? unscaledDeltaTime * _gameSpeedMultiplier
            : unscaledDeltaTime;
    }

    private void Log(string message)
    {
        if (_logger != null)
            _logger.Invoke(message);
        else
            Debug.Log(message);
    }
}
