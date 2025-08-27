// Verse_WorldComponent_GravshipController_LandingEnded.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

#if !v1_5
namespace ColonyManagerRedux;

[HarmonyPatch(typeof(WorldComponent_GravshipController), "LandingEnded")]
internal static class Verse_WorldComponent_GravshipController_LandingEnded { }
#endif // !v1_5
