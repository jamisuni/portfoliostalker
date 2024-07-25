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

namespace Pfs.Types;

// Part of ''Overview'' -account feature, this presents different spinner groups
public class OverviewGroupsData
{
    public string Name { get; set; }

    public string LimitSinglePf { get; set; } = "";  // set if group limited to single PF, empty = all

    public List<string> SRefs { get; set; } = new();

    public decimal HcTotalInvested { get; set; } = 0;
    public decimal HcTotalValuation { get; set; } = 0;
    public decimal HcGrowthP { get; set; } = 0; // from Val-Inv/Inv
    public decimal HcPrevValuation { get; set; } = 0;
    public decimal HcPrevValP { get; set; } = 0;
}
