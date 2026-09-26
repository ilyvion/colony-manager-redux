// ShortcutCategory.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// A shortcut row that toggles every option matching <paramref name="Predicate"/>.
/// </summary>
/// <param name="LabelKey">The translation key for the row label.</param>
/// <param name="TipKey">The translation key for the row tooltip.</param>
/// <param name="Predicate">Selects the options this row toggles.</param>
public sealed record ShortcutCategory<T>(string LabelKey, string TipKey, Func<T, bool> Predicate);
