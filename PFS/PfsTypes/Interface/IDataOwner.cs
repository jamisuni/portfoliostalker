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

    // No loading from anywhere, just 'clean' defaults
    void OnInitDefaults();

    // Part of startup cycle, does use local stored data or inits to defaults
    List<string> OnLoadStorage();

    // FE controls saving of all data thru this
    void OnSaveStorage();

    string CreateBackup();

    string CreatePartialBackup(List<string> symbols);

    List<string> RestoreBackup(string content);

    /* !!!DOCUMENT!!! Data Handling
     * 
     * - Startup of application:
     *      - Components constructors init to default clear state. They are separately called to load stored content.
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
     *      - Saving is automatic, as easiest way to load new tuff is to do full reload from memory where
     *        all data was just stored.. making flow to identical on normal launch, and restore cases.
     */
}
