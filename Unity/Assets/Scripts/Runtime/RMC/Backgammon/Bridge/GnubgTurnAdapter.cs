using System;
using System.Collections.Generic;
using EngineCore;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Bridge
{
    public static class GnubgTurnAdapter
    {
        public static Turn ParseMove(string gnubgMoveString, GameState originalState)
        {
            Debug.Log($"[GnubgTurnAdapter] ========== PARSING GNUBG MOVE ==========");
            Debug.Log($"[GnubgTurnAdapter] Move string: '{gnubgMoveString}'");
            Debug.Log($"[GnubgTurnAdapter] Player: {originalState.PlayerOnRoll}, Dice: {originalState.Dice1}/{originalState.Dice2}");

            var turn = new Turn { Moves = new List<Move>() };

            if (string.IsNullOrWhiteSpace(gnubgMoveString))
            {
                Debug.LogWarning("[GnubgTurnAdapter] Empty move string!");
                return turn;
            }

            string[] moveParts = gnubgMoveString.Split(new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            Debug.Log($"[GnubgTurnAdapter] Split into {moveParts.Length} move parts");

            GameState simulatedState = CloneGameState(originalState);

            int moveIndex = 0;
            foreach (string movePart in moveParts)
            {
                // Handle repeat notation: "13/11(2)" means repeat "13/11" twice
                string cleanMovePart = movePart.TrimEnd(',', ';', '.');
                int repeatCount = 1;

                var match = System.Text.RegularExpressions.Regex.Match(cleanMovePart, @"^(.+?)\((\d+)\)$");
                if (match.Success)
                {
                    cleanMovePart = match.Groups[1].Value;
                    repeatCount = int.Parse(match.Groups[2].Value);
                    Debug.Log($"[GnubgTurnAdapter] Found repeat annotation: '{movePart}' = '{cleanMovePart}' x{repeatCount}");
                }

                // Split by '/' to get all points in the sequence
                // Simple move: "13/11" → ["13", "11"]
                // Compound move: "24/23*/22" → ["24", "23*", "22"]
                string[] points = cleanMovePart.Split('/');
                if (points.Length < 2)
                {
                    Debug.LogWarning($"[GnubgTurnAdapter] Invalid move format: {cleanMovePart}");
                    continue;
                }

                // Expand compound notation into consecutive move pairs
                // "24/23*/22" becomes: two separate moves
                if (points.Length > 2)
                {
                    Debug.Log($"[GnubgTurnAdapter] Compound move detected: '{cleanMovePart}' has {points.Length} points");
                }

                // Process each move segment, repeating if needed
                for (int rep = 0; rep < repeatCount; rep++)
                {
                    for (int i = 0; i < points.Length - 1; i++)
                    {
                        string fromPoint = points[i].TrimEnd('*', '!', '?');
                        string toPoint = points[i + 1];
                        bool isHit = toPoint.EndsWith("*");
                        toPoint = toPoint.TrimEnd('*', '!', '?');

                        if (!TryConvertGnubgNotationToNumeric(fromPoint, out int gnubgFrom) ||
                            !TryConvertGnubgNotationToNumeric(toPoint, out int gnubgTo))
                        {
                            Debug.LogWarning($"[GnubgTurnAdapter] Failed to parse move points: {fromPoint}/{toPoint}");
                            continue;
                        }

                        int engineFrom = ConvertGnubgPointToEngine(gnubgFrom, simulatedState.PlayerOnRoll);
                        int engineTo = ConvertGnubgPointToEngine(gnubgTo, simulatedState.PlayerOnRoll);

                        Debug.Log($"[GnubgTurnAdapter] Move {moveIndex}: GNUBG '{fromPoint}/{toPoint}'{(isHit ? "*" : "")} -> Engine {engineFrom}→{engineTo}{(isHit ? " (HIT)" : "")}");

                        turn.Moves.Add(new Move
                        {
                            From = engineFrom,
                            To = engineTo,
                            IsHit = isHit
                        });

                        ApplyMoveToSimulation(simulatedState, engineFrom, engineTo, isHit);
                        moveIndex++;
                    }
                }
            }

            turn.ResultingState = simulatedState;
            Debug.Log($"[GnubgTurnAdapter] Parsed {turn.Moves.Count} moves total");
            Debug.Log($"[GnubgTurnAdapter] Final bar state - Player1: {simulatedState.Player1Checkers[24]}, Player2: {simulatedState.Player2Checkers[24]}");
            Debug.Log($"[GnubgTurnAdapter] ==========================================");
            return turn;
        }

        private static bool TryConvertGnubgNotationToNumeric(string notation, out int numericPoint)
        {
            // Handle special keywords (case-insensitive)
            string lower = notation.ToLowerInvariant();
            if (lower == "bar")
            {
                numericPoint = 25;
                Debug.Log($"[GnubgTurnAdapter] Converted notation 'bar' -> 25");
                return true;
            }
            if (lower == "off")
            {
                numericPoint = 0;
                Debug.Log($"[GnubgTurnAdapter] Converted notation 'off' -> 0");
                return true;
            }

            // Try parsing as numeric
            return int.TryParse(notation, out numericPoint);
        }

        private static int ConvertGnubgPointToEngine(int gnubgPoint, int playerOnRoll)
        {
            // Special points (same for both players)
            if (gnubgPoint == 25) return 24;  // Bar
            if (gnubgPoint == 0) return -1;   // Off (bearing off)

            // Player 1 (engine index 0): GNUBG 24→1 maps to engine 23→0
            if (playerOnRoll == 0)
            {
                return gnubgPoint - 1;  // Direct mapping
            }
            // Player 2 (engine index 1): GNUBG 24→1 maps to engine 0→23 (REVERSED)
            else
            {
                return 25 - gnubgPoint;  // Flip the board
            }
        }


        private static void ApplyMoveToSimulation(GameState state, int from, int to, bool isHit)
        {
            int playerOnRoll = state.PlayerOnRoll;

            if (playerOnRoll == 0)
            {
                // Remove checker from source position
                if (from >= 0 && from < 25)
                    state.Player1Checkers[from]--;

                if (to >= 0 && to < 24)
                {
                    // If hit, send opponent checker to bar
                    if (isHit && state.Player2Checkers[to] == 1)
                    {
                        state.Player2Checkers[to] = 0;
                        state.Player2Checkers[24]++;  // Bar for Player 2
                        Debug.Log($"[GnubgTurnAdapter] Hit detected at point {to}! Player2 bar count now: {state.Player2Checkers[24]}");
                    }
                    else if (isHit)
                    {
                        Debug.LogWarning($"[GnubgTurnAdapter] Hit marker present but no opponent checker at point {to}! Player2Checkers[{to}]={state.Player2Checkers[to]}");
                    }
                    // Place our checker on destination
                    state.Player1Checkers[to]++;
                }
            }
            else
            {
                // Remove checker from source position
                if (from >= 0 && from < 25)
                    state.Player2Checkers[from]--;

                if (to >= 0 && to < 24)
                {
                    // If hit, send opponent checker to bar
                    if (isHit && state.Player1Checkers[to] == 1)
                    {
                        state.Player1Checkers[to] = 0;
                        state.Player1Checkers[24]++;  // Bar for Player 1
                        Debug.Log($"[GnubgTurnAdapter] Hit detected at point {to}! Player1 bar count now: {state.Player1Checkers[24]}");
                    }
                    else if (isHit)
                    {
                        Debug.LogWarning($"[GnubgTurnAdapter] Hit marker present but no opponent checker at point {to}! Player1Checkers[{to}]={state.Player1Checkers[to]}");
                    }
                    // Place our checker on destination
                    state.Player2Checkers[to]++;
                }
            }
        }

        private static GameState CloneGameState(GameState original)
        {
            var clone = new GameState
            {
                CubeValue = original.CubeValue,
                CubeOwner = original.CubeOwner,
                PlayerOnRoll = original.PlayerOnRoll,
                PlayerToDecide = original.PlayerToDecide,
                Dice1 = original.Dice1,
                Dice2 = original.Dice2,
                MatchLength = original.MatchLength,
                Player1Score = original.Player1Score,
                Player2Score = original.Player2Score,
                Player1Checkers = new int[25],
                Player2Checkers = new int[25]
            };

            Array.Copy(original.Player1Checkers, clone.Player1Checkers, 25);
            Array.Copy(original.Player2Checkers, clone.Player2Checkers, 25);

            return clone;
        }
    }
}
