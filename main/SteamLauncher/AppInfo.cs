using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using ValveKeyValue;
using Microsoft.Win32;

using System.Text.Json;
using System.IO;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Buffers;
using System.Text;
namespace SteamLauncher
{
    public class AppInfo
    {
        private const uint Magic29 = 0x07_56_44_29;
        private const uint Magic28 = 0x07_56_44_28;
        private const uint Magic = 0x07_56_44_27;

        public string? AppId { get; set; }
        public string? LibraryPath { get; set; }

        public string? ManifestPatH { get; set; }

        public string? InstallPath { get; set; }

        public string? Name { get; set; }

        public string? ClientIcon { get; set; }

        public string? Type { get; set; }

        public string? Logo { get; set; }
        public BitmapImage? Icon { get; private set; }

        public DateTime LastPlayed { get; set;  }

        private static List<AppInfo>? cached_results;
        private static DateTime cache_date;

        public static IEnumerable<AppInfo> GetApps(bool force = false)
        {
            var steamPath = GetSteamPath();

            if (steamPath == null)
            {
                throw new Exception("Unable to locate steam library.");
            }

            var appinfopath = Path.Join(steamPath, "appcache", "appinfo.vdf");
            var libraryfolderspath = Path.Join(steamPath, "steamapps", "libraryfolders.vdf");

            //var bytes = System.IO.File.ReadAllBytes(appinfopath);
            
            var date = System.IO.Directory.GetLastWriteTime(libraryfolderspath);
            if (!force && date == cache_date)
            {
                return cached_results;
            }
            else
                cache_date = date;
            
            LoadApps(appinfopath, libraryfolderspath);

            return cached_results;
        }

        private static void LoadApps(string appinfopath, string libraryfolderspath)
        {

            var deserializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
            var options = new KVSerializerOptions();
            var textdeserializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            KVDocument folderconfig;
            using (var foldersstream = System.IO.File.OpenRead(libraryfolderspath))
                folderconfig = textdeserializer.Deserialize(foldersstream);

            var steamapps = cached_results = new List<AppInfo>();


            foreach (var library in folderconfig)
            {
                var libpath = library.FirstOrDefault(p => p.Name == "path")?.Value.ToString()?.Replace(Path.DirectorySeparatorChar.ToString() + Path.DirectorySeparatorChar.ToString(), Path.DirectorySeparatorChar.ToString());
                var appslist = library.FirstOrDefault(p => p.Name == "apps")?.Select(p => p.Name);


                if (libpath != null && appslist != null)
                {
                    foreach (var appid in appslist)
                        steamapps.Add(new AppInfo() { AppId = appid, LibraryPath = libpath });
                }
            }

            foreach (var steamapp in steamapps)
            {
                var manifestpath = Path.Join(steamapp.LibraryPath, "steamapps", $"appmanifest_{steamapp.AppId}.acf");
                steamapp.ManifestPatH = manifestpath;
                using (var manifeststream = System.IO.File.OpenRead(manifestpath))
                {
                    var manifest = textdeserializer.Deserialize(manifeststream);
                    steamapp.InstallPath = Path.Join(steamapp.LibraryPath, "steamapps", "common", manifest.First(k => k.Name == "installdir").Value.ToString());
                    steamapp.Name = manifest.First(m => m.Name == "name").Value.ToString();
                    steamapp.LastPlayed = DateTimeFromUnixTime(uint.Parse(manifest.FirstOrDefault(p => p?.Name == "LastPlayed")?.Value?.ToString() ?? "0"));

                }
            }

            using (var stream = System.IO.File.OpenRead(appinfopath))// new MemoryStream(bytes)) 
            using (var reader = new BinaryReader(stream))
            {

                var magic = reader.ReadUInt32();
                var universal = reader.ReadUInt32();
                uint appid = 0;

                //var appids = (from app in apps select app.AppId).ToArray();
                if (magic == Magic29)
                {
                    var stringTableOffset = reader.ReadInt64();
                    var offset = reader.BaseStream.Position;
                    reader.BaseStream.Position = stringTableOffset;
                    var stringCount = reader.ReadUInt32();
                    var stringPool = new string[stringCount];

                    for (var i = 0; i < stringCount; i++)
                    {
                        stringPool[i] = ReadNullTermUtf8String(reader.BaseStream);
                    }

                    reader.BaseStream.Position = offset;

                    //options.StringPool = stringPool;
                }

                while ((appid = reader.ReadUInt32()) != 0)
                {
                    var size = reader.ReadUInt32();
                    var steamapp = steamapps.FirstOrDefault(a => a.AppId == appid.ToString());
                    if (steamapp != null)
                    {
                        var InfoState = reader.ReadUInt32();
                        var LastUpdated = DateTimeFromUnixTime(reader.ReadUInt32());
                        var Token = reader.ReadUInt64();
                        var Hash = new ReadOnlyCollection<byte>(reader.ReadBytes(20));
                        var ChangeNumber = reader.ReadUInt32();
                        var BinaryDataHash = new ReadOnlyCollection<byte>(reader.ReadBytes(20));
                        var vdf = deserializer.Deserialize(stream, options);

                        var common = vdf.FirstOrDefault(v => v.Name == "common");
                        if (common != null)
                        {
                            steamapp.ClientIcon = common.FirstOrDefault(p => p.Name == "clienticon")?.Value.ToString();
                            steamapp.Type = common.First(p => p.Name == "type").Value.ToString();
                            steamapp.Logo = common.FirstOrDefault(p => p.Name == "logo")?.Value.ToString();
                            var imagePath = steamapp.ClientIcon != null ? $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{steamapp.AppId}/{steamapp.ClientIcon}.ico" : steamapp.Logo != null ? $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{steamapp.AppId}/{steamapp.Logo}.jpg" : "";

                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                var bitmapImage = new BitmapImage(new Uri(imagePath));
                                steamapp.Icon = bitmapImage;
                            }
                        }

                    }
                    else
                        stream.Position = stream.Position + size;
                }
            }
        }
        public static string GetAppsJson()
        {
            return JsonSerializer.Serialize(GetApps(), typeof(List<AppInfo>), new JsonSerializerOptions() { WriteIndented = true });
        }

        private static string? GetSteamPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam") ??
                          RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                              .OpenSubKey("SOFTWARE\\Valve\\Steam");

                if (key != null && key.GetValue("SteamPath") is string steamPath)
                {
                    return steamPath;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var paths = new[] { ".steam", ".steam/steam", ".steam/root", ".local/share/Steam" };

                return paths
                    .Select(path => Path.Join(home, path))
                    .FirstOrDefault(steamPath => Directory.Exists(Path.Join(steamPath, "appcache")));
            }
            else if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Join(home, "Steam");
            }

            throw new PlatformNotSupportedException();
        }

        private static DateTime DateTimeFromUnixTime(uint unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }

        private static string ReadNullTermUtf8String(Stream stream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(32);

            try
            {
                var position = 0;

                do
                {
                    var b = stream.ReadByte();

                    if (b <= 0) // null byte or stream ended
                    {
                        break;
                    }

                    if (position >= buffer.Length)
                    {
                        var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = newBuffer;
                    }

                    buffer[position++] = (byte)b;
                }
                while (true);

                return Encoding.UTF8.GetString(buffer[..position]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}