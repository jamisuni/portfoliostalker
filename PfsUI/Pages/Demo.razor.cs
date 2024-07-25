/*
 * Copyright (C) 2024 Jami Suni
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

using Microsoft.AspNetCore.Components;

using MudBlazor;
using Pfs.Types;
using PfsUI.Components;
using PfsUI.Layout;

namespace PfsUI.Pages;

public partial class Demo
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] PfsUiState PfsUiState { get; set; }
    [Inject] IDialogService Dialog { get; set; }
    [CascadingParameter] public MainLayout Layout { get; set; }

    [Parameter] public int Id { get; set; } = 0;

    protected override void OnInitialized()
    {

    }

    protected override void OnParametersSet()
    {
        var demo = BlazorPlatform.SetDemo(Id);

        if (demo.demoZip == null)
            return;

        Result res = Pfs.Account().LoadDemo(demo.demoZip);

        Layout.NavigateToHome();
    }
}
