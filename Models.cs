// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.
//
// Raytolfas Launcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Raytolfas Launcher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Raytolfas Launcher. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;

namespace RaytolfasLauncher
{
    public class AccountData
    {
        public string Username { get; set; } = "";
        public string UUID { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string Type { get; set; } = "Offline"; 
    }

    public class LauncherSettings
    {
        public List<AccountData> Accounts { get; set; } = new List<AccountData>();
        public int SelectedAccountIndex { get; set; } = 0;
        public int SelectedRam { get; set; } = 4096;
        public string MinecraftPath { get; set; } = "";
        
        public bool ShowReleases { get; set; } = true;
        public bool ShowSnapshots { get; set; } = false;
        public bool ShowModded { get; set; } = true;

        public bool ShowDiscordStatus { get; set; } = true;
        public bool HideLauncherOnPlay { get; set; } = true;
        
        public string CustomBackgroundPath { get; set; } = "";
    }

    public class ServerInfo
    {
        public string Name { get; set; } = "";
        public string IP { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string OnlineStatus => "ИНФО"; 
        public string PlayersCount => Version;
    }
}