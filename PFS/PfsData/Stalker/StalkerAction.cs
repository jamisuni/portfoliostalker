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

using Pfs.Types;

namespace Pfs.Data.Stalker;

// Holder of Action, that is single command w Operation-Element combo plus set of parameters
public class StalkerAction
{
    // Each actions is defined w combo of Operation-Element.. example 'Add-Alarm' 
    public StalkerOperation Operation { get; internal set; } = StalkerOperation.Unknown;
    public StalkerElement Element { get; internal set; } = StalkerElement.Unknown;

    // And AllowedCombo, has one or multiple parameters it requires to receive (atm doesnt support zero parameters)
    public List<StalkerParam> Parameters { get; internal set; } = new();

    // Hidden constructor
    protected StalkerAction() { }

    // Always to be created with factory type method to get properly initialized, or null for failure (= not supported combo)
    public static StalkerAction Create(StalkerOperation operation, StalkerElement element)
    {
        StalkerAction ret = new();

        ret.Operation = operation;
        ret.Element = element;

        if (operation == StalkerOperation.Unknown || element == StalkerElement.Unknown)
            return null;

        // Per Operation-Element combo lets get template descripting all expected parameters for this StalkerAction
        string[] expectedParameters = StalkerActionTemplate.Get(operation, element);

        if (expectedParameters == null || expectedParameters.Count() == 0)
            // This operation is not supported
            return null;

        foreach (string paramTemplate in expectedParameters)
            // And create all parameter templates, so after creation its ready to accept parameters
            ret.Parameters.Add(new StalkerParam(paramTemplate));

        // If got this far, then its acceptable combo w now all parameters initialized w templates but not yet set
        return ret;
    }

    // Allows to check if has all parameters properly set w valid values, and ready for action
    public Result IsReady()
    {
        foreach (StalkerParam param in Parameters)
            if (param.Error.Ok == false)
                return param.Error;

        return new OkResult();
    }

    public Result SetParam(string input)
    {
        string[] splitParam = input.Split('=');

        if (splitParam.Count() != 2)
            return new FailResult($"{input} is not on required format: param=value");

        foreach (StalkerParam param in Parameters )
            if ( param.Name == splitParam[0] )
                // Found correct parameter, so lets set/parse it..
                return param.Parse(splitParam[1]);

        return new FailResult($"{input} could nor find this param from template?");
    }

    // Little helper to allow access parameter w parameter Name
    public StalkerParam Param(string name) // Would be possible to 'string this[string name]' -> access w action['Stock'] .. but maybe too confusing
    {
        return Parameters.Single(p => p.Name == name); 
    }

    public static string Help()
    {
        return StalkerActionTemplate.Help();
    }
}
