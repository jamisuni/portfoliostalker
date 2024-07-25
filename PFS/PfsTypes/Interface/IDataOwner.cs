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

// Each component that does saving to local storage & caching registers this for central events
public interface IDataOwner
{
    event EventHandler<string> EventNewUnsavedContent; // Sends ComponentName as parameter

    string GetComponentName();

    // Used example after CleanUpAll to reinitialize components
    void OnDataInit();

    string CreateBackup();

    string CreatePartialBackup(List<string> symbols);

    Result RestoreBackup(string content);

    // FE controls saving of all data thru this
    void OnDataSaveStorage();

    /* !!!DOCUMENT!!! Data Handling
     * 
     * - Startup of application:
     *      - All components are doing init/loading when they get called, no central control
     * 
     * - Clean up everything:
     *      - PFS.Client does 'PermClearAll' that deletes all data from local storage
     *      - FE is also finishing this with enforced application loading
     *      => everything ends default empty as application reload reads empty data and inits all
     * 
     * - Future! Login as different user
     *      - If 'demo' or 'non-local-storing' account on new then does init and reloads from online backup. 
     *        As 'no-storing' is kept on a main accounts local data can just hide under waiting.
     *      - If 'normal-local-storing' account then login fails if has existing local data in use, and 
     *        gives warning that need first clear all before can login to fully different user.
     *        => 'OnDataClearStorage' is removed, not to be used anymore this case!
     * 
     * - Restore backup:
     *      - 
     *      - Todo! Saving shouldnt be automatic after this, but instead user controls it.
     */
}
