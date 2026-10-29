// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Client.News.Conditions;

[ImplicitDataDefinitionForInheritors]
public abstract partial class NewsCondition
{
    public abstract bool Check(IDependencyCollection deps);
}
