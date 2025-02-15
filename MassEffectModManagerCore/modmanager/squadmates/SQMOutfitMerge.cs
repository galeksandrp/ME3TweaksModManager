﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using MassEffectModManagerCore.modmanager.gameini;
using MassEffectModManagerCore.modmanager.helpers;
using MassEffectModManagerCore.modmanager.localizations;
using MassEffectModManagerCore.modmanager.mergedlc;
using MassEffectModManagerCore.modmanager.objects;
using MassEffectModManagerCore.modmanager.usercontrols;
using MassEffectModManagerCore.modmanager.windows;
using Newtonsoft.Json;
using Serilog;

namespace MassEffectModManagerCore.modmanager.squadmates
{
    public class SQMOutfitMerge
    {
        private const string SQUADMATE_MERGE_MANIFEST_FILE = @"SquadmateMergeInfo.sqm";
        public const int STARTING_OUTFIT_CONDITIONAL = 10000;

        internal class SquadmateMergeInfo
        {
            [JsonProperty(@"game")]
            public MEGame Game { get; set; }

            [JsonProperty(@"outfits")]
            public List<SquadmateInfoSingle> Outfits { get; set; }

            public bool Validate(string dlcName, GameTarget target, CaseInsensitiveDictionary<string> loadedFiles)
            {
                foreach (var outfit in Outfits)
                {
                    // Check packages
                    if (!loadedFiles.ContainsKey($@"{outfit.HenchPackage}.pcc"))
                    {
                        Log.Error($@"SquadmateMergeInfo failed validation: {outfit.HenchPackage}.pcc not found in game");
                        return false;
                    }

                    if (Game.IsGame3())
                    {
                        if (!loadedFiles.ContainsKey($@"{outfit.HenchPackage}_Explore.pcc"))
                        {
                            Log.Error($@"SquadmateMergeInfo failed validation: {outfit.HenchPackage}_Explore.pcc not found in game");
                            return false;
                        }
                    }

                    if (!loadedFiles.ContainsKey($@"SFXHenchImages_{dlcName}.pcc"))
                    {
                        Log.Error($@"SquadmateMergeInfo failed validation: SFXHenchImages_{dlcName}.pcc not found in game");
                        return false;
                    }
                }

                return true;
            }
        }


        public class SquadmateInfoSingle
        {
            [JsonProperty(@"henchname")]
            public string HenchName { get; set; }

            [JsonProperty(@"henchpackage")]
            public string HenchPackage { get; set; }

            [JsonProperty(@"highlightimage")]
            public string HighlightImage { get; set; }

            [JsonProperty(@"availableimage")]
            public string AvailableImage { get; set; }

            //[JsonProperty(@"deadimage")]
            //public string DeadImage { get; set; }

            [JsonProperty(@"silhouetteimage")]
            public string SilhouetteImage { get; set; }

            [JsonProperty(@"descriptiontext0")]
            public int DescriptionText0 { get; set; }

            [JsonProperty(@"customtoken0")]
            public int CustomToken0 { get; set; }

            /// <summary>
            /// The index of the conditional function to check if this outfit is the selected one when loading
            /// </summary>
            [JsonIgnore]
            public int ConditionalIndex { get; set; }

            /// <summary>
            /// The outfit index that uniquely identifies this outfit
            /// </summary>
            [JsonIgnore]
            public int AppearanceId { get; set; }

            /// <summary>
            /// The outfit index that is set in the conditionals to define this outfit
            /// </summary>
            [JsonIgnore]
            public int MemberAppearanceValue { get; set; }

            /// <summary>
            /// Used to add values to inis
            /// </summary>
            [JsonIgnore]
            public string DLCName { get; set; }

            public Dictionary<string, string> ToPropertyDictionary()
            {
                var dict = new Dictionary<string, string>
                {
                    [@"AppearanceId"] = AppearanceId.ToString(),
                    [@"MemberAppearanceValue"] = MemberAppearanceValue.ToString(),
                    [@"MemberTag"] = $@"hench_{HenchName.ToLower()}",
                    [@"MemberAppearancePlotLabel"] = $@"Appearance{HenchName}",
                    [@"HighlightImage"] = HighlightImage,
                    [@"AvailableImage"] = AvailableImage,
                    [@"DeadImage"] = @"GUI_Henchmen_Images.PlaceHolder", // Game 3
                    [@"SilhouetteImage"] = SilhouetteImage,
                    [@"DescriptionText[0]"] = DescriptionText0.ToString(),
                    [@"CustomToken0[0]"] = CustomToken0.ToString()
                };
                return dict;
            }
        }


        private static StructProperty GeneratePlotStreamingElement(string packageName, int conditionalNum)
        {
            PropertyCollection pc = new PropertyCollection();
            pc.AddOrReplaceProp(new NameProperty(packageName, @"ChunkName"));
            pc.AddOrReplaceProp(new IntProperty(conditionalNum, @"Conditional"));
            pc.AddOrReplaceProp(new BoolProperty(false, @"bFallback"));
            pc.AddOrReplaceProp(new NoneProperty());

            return new StructProperty(@"PlotStreamingElement", pc);
        }

        private static int GetSquadmateOutfitInt(string squadmateName, MEGame game)
        {
            Log.Information($@"SQMMERGE: Generating outfit int for {game} {squadmateName}");
            if (game.IsGame2())
            {
                switch (squadmateName)
                {
                    case @"Convict": return 314;
                    case @"Garrus": return 318;
                    case @"Geth": return 315;
                    case @"Grunt": return 322;
                    case @"Leading": return 313;
                    case @"Mystic": return 323;
                    case @"Professor": return 321;
                    case @"Tali": return 320;
                    case @"Thief": return 317;
                    case @"Veteran": return 324;
                    case @"Vixen": return 312;
                }
            }
            else if (game.IsGame3())
            {
                switch (squadmateName)
                {
                    case @"Liara": return 10152;
                    case @"Kaidan": return 10153;
                    case @"Ashley": return 10154;
                    case @"Garrus": return 10155;
                    case @"EDI": return 10156;
                    case @"Prothean": return 10157;
                    case @"Marine": return 10158;
                    case @"Tali": return 10214;
                        // case @"Wrex": return ??; Wrex outfit can't be changed, its hardcoded to 13, which is TRUE
                }
            }

            throw new Exception(M3L.GetString(M3L.string_interp_invalidHenchNameSquadmateNameValueIsCaseSensitive, squadmateName));
        }

        public static void BuildBioPGlobal(GameTarget target)
        {
            M3MergeDLC.RemoveMergeDLC(target);
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, gameRootOverride: target.TargetPath);
            //var mergeFiles = loadedFiles.Where(x =>
            //    x.Key.StartsWith(@"BioH_") && x.Key.Contains(@"_DLC_MOD_") && x.Key.EndsWith(@".pcc") && !x.Key.Contains(@"_LOC_") && !x.Key.Contains(@"_Explore."));

            Log.Information($@"SQMMERGE: Building BioP_Global");
            var appearanceInfo = new CaseInsensitiveDictionary<List<SquadmateInfoSingle>>();

            int appearanceId = 255; // starting
            int currentConditional = STARTING_OUTFIT_CONDITIONAL;

            // Scan squadmate merge files
            var sqmSupercedances = M3Directories.GetFileSupercedances(target, new[] { @".sqm" });
            if (sqmSupercedances.TryGetValue(SQUADMATE_MERGE_MANIFEST_FILE, out var infoList))
            {
                infoList.Reverse();
                foreach (var dlc in infoList)
                {
                    Log.Information($@"SQMMERGE: Processing {dlc}");

                    var jsonFile = Path.Combine(M3Directories.GetDLCPath(target), dlc, target.Game.CookedDirName(), SQUADMATE_MERGE_MANIFEST_FILE);
                    var infoPackage = JsonConvert.DeserializeObject<SquadmateMergeInfo>(File.ReadAllText(jsonFile));
                    if (!infoPackage.Validate(dlc, target, loadedFiles))
                    {
                        continue; // skip this
                    }

                    // Enumerate all outfits listed for a single squadmate
                    foreach (var outfit in infoPackage.Outfits)
                    {
                        List<SquadmateInfoSingle> list;

                        // See if we already have an outfit list for this squadmate, maybe from another mod...
                        if (!appearanceInfo.TryGetValue(outfit.HenchName, out list))
                        {
                            list = new List<SquadmateInfoSingle>();
                            appearanceInfo[outfit.HenchName] = list;
                        }

                        outfit.ConditionalIndex = currentConditional++; // This is always incremented, so it might appear out of order in game files depending on how mod order is processed, that should be okay though.
                        outfit.AppearanceId = appearanceId++; // may need adjusted
                        outfit.DLCName = dlc;
                        list.Add(outfit);
                        Log.Information($@"SQMMERGE: ConditionalIndex for {outfit.HenchName} appearanceid {outfit.AppearanceId}: {outfit.ConditionalIndex}");
                    }
                }
            }

            if (appearanceInfo.Any())
            {
                var biopGlobal = MEPackageHandler.OpenMEPackage(loadedFiles[@"BioP_Global.pcc"]);
                var lsk = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"LevelStreamingKismet");
                var persistentLevel = biopGlobal.FindExport(@"TheWorld.PersistentLevel");

                // Clone LevelStreamingKismets
                foreach (var sqm in appearanceInfo.Values)
                {
                    foreach (var outfit in sqm)
                    {
                        var fName = outfit.HenchPackage;
                        var newLSK = EntryCloner.CloneEntry(lsk);
                        newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));

                        if (target.Game.IsGame3())
                        {
                            // Game 3 has _Explore files too
                            fName += @"_Explore";
                            newLSK = EntryCloner.CloneEntry(lsk);
                            newLSK.WriteProperty(new NameProperty(fName, @"PackageName"));
                        }
                    }
                }

                // Update BioWorldInfo
                // Doesn't have consistent number so we can't find it by instanced full path
                var bioWorldInfo = biopGlobal.Exports.FirstOrDefault(x => x.ClassName == @"BioWorldInfo");

                var props = bioWorldInfo.GetProperties();

                // Update Plot Streaming
                var plotStreaming = props.GetProp<ArrayProperty<StructProperty>>(@"PlotStreaming");
                foreach (var sqm in appearanceInfo.Values)
                {
                    foreach (var outfit in sqm)
                    {
                        // find item to add to
                        buildPlotElementObject(plotStreaming, outfit, target.Game, false);
                        if (target.Game.IsGame3())
                        {
                            buildPlotElementObject(plotStreaming, outfit, target.Game, true);
                        }
                    }
                }


                // Update StreamingLevels
                var streamingLevels = props.GetProp<ArrayProperty<ObjectProperty>>(@"StreamingLevels");
                streamingLevels.ReplaceAll(biopGlobal.Exports.Where(x => x.ClassName == @"LevelStreamingKismet").Select(x => new ObjectProperty(x)));

                bioWorldInfo.WriteProperties(props);


                M3MergeDLC.GenerateMergeDLC(target, Guid.NewGuid());

                // Save BioP_Global into DLC
                var cookedDir = Path.Combine(M3Directories.GetDLCPath(target), M3MergeDLC.MERGE_DLC_FOLDERNAME, target.Game.CookedDirName());
                var outP = Path.Combine(cookedDir, @"BioP_Global.pcc");
                biopGlobal.Save(outP);

                // Generate conditionals file
                if (target.Game.IsGame3())
                {
                    CNDFile cnd = new CNDFile();
                    cnd.ConditionalEntries = new List<CNDFile.ConditionalEntry>();

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var scText = $@"(plot.ints[{GetSquadmateOutfitInt(outfit.HenchName, target.Game)}] == i{outfit.MemberAppearanceValue})";
                            var compiled = ME3ConditionalsCompiler.Compile(scText);
                            cnd.ConditionalEntries.Add(new CNDFile.ConditionalEntry()
                            { Data = compiled, ID = outfit.ConditionalIndex });
                        }
                    }

                    cnd.ToFile(Path.Combine(cookedDir, $@"Conditionals{M3MergeDLC.MERGE_DLC_FOLDERNAME}.cnd"));
                }
                else if (target.Game.IsGame2())
                {
                    var startupF = Path.Combine(cookedDir, $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc");
                    var startup = MEPackageHandler.OpenMEPackageFromStream(Utilities.GetResourceStream(
                        $@"MassEffectModManagerCore.modmanager.mergedlc.{target.Game}.Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc"));
                    var conditionalClass =
                        startup.FindExport($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals");

                    // Add Conditional Functions
                    FileLib fl = new FileLib(startup);
                    bool initialized = fl.Initialize(new RelativePackageCache() { RootPath = M3Directories.GetBioGamePath(target) });
                    if (!initialized)
                    {
                        throw new Exception(
                            $@"FileLib for script update could not initialize, cannot install conditionals");
                    }


                    var funcToClone =
                        startup.FindExport($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals.TemplateFunction");
                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var func = EntryCloner.CloneEntry(funcToClone);
                            func.ObjectName = $@"F{outfit.ConditionalIndex}";
                            func.indexValue = 0;

                            var scText = new StreamReader(Utilities.GetResourceStream(
                                    $@"MassEffectModManagerCore.modmanager.squadmates.{target.Game}.HasOutfitOnConditional.txt"))
                                .ReadToEnd();

                            scText = scText.Replace(@"%CONDITIONALNUM%", outfit.ConditionalIndex.ToString());
                            scText = scText.Replace(@"%SQUADMATEOUTFITPLOTINT%", outfit.AppearanceId.ToString());
                            scText = scText.Replace(@"%OUTFITINDEX%", outfit.MemberAppearanceValue.ToString());

                            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(func, scText, fl);
                            if (log.AllErrors.Any())
                            {
                                Log.Error($@"Error compiling function {func.InstancedFullPath}:");
                                foreach (var l in log.AllErrors)
                                {
                                    Log.Error(l.Message);
                                }

                                throw new Exception(M3L.GetString(M3L.string_interp_errorCompilingConditionalFunction, func, string.Join('\n', log.AllErrors.Select(x => x.Message))));
                            }
                        }
                    }


                    // Relink the conditionals chain
                    UClass uc = ObjectBinary.From<UClass>(conditionalClass);
                    uc.UpdateLocalFunctions();
                    uc.UpdateChildrenChain();
                    conditionalClass.WriteBinary(uc);

                    startup.Save(startupF);
                }


                // Add startup package, member appearances
                if (target.Game.IsGame2())
                {
                    var bioEngine = Path.Combine(cookedDir, @"BIOEngine.ini");
                    var ini = DuplicatingIni.LoadIni(bioEngine);

                    var startupSection = ini.GetOrAddSection(@"Engine.StartupPackages");

                    startupSection.Entries.Add(new DuplicatingIni.IniEntry(@"+DLCStartupPackage", $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}"));
                    startupSection.Entries.Add(new DuplicatingIni.IniEntry(@"+Package", $@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}"));

                    ini.WriteToFile(bioEngine);


                }
                else if (target.Game.IsGame3())
                {
                    var mergeCoalFile = Path.Combine(M3Directories.GetDLCPath(target), M3MergeDLC.MERGE_DLC_FOLDERNAME, target.Game.CookedDirName(), $@"Default_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.bin");
                    var mergeCoal = CoalescedConverter.DecompileGame3ToMemory(new MemoryStream(File.ReadAllBytes(mergeCoalFile)));

                    // Member appearances
                    var bioUiDoc = XDocument.Parse(mergeCoal[@"BIOUI.xml"]);

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            var entry = new Game3CoalescedValueEntry()
                            {
                                Section = @"sfxgame.sfxguidata_teamselect",
                                Name = @"selectappearances",
                                Type = 3,
                                Value = StringStructParser.BuildCommaSeparatedSplitValueList(outfit.ToPropertyDictionary(), @"AvailableImage", @"HighlightImage", @"DeadImage", @"SilhouetteImage")
                            };
                            Game3CoalescedHelper.AddArrayEntry(bioUiDoc, entry);
                        }
                    }

                    mergeCoal[@"BIOUI.xml"] = bioUiDoc.ToString();

                    // Dynamic load mapping
                    var bioEngineDoc = XDocument.Parse(mergeCoal[@"BIOEngine.xml"]);

                    foreach (var sqm in appearanceInfo.Values)
                    {
                        foreach (var outfit in sqm)
                        {
                            //
                            // * <Section name="sfxgame.sfxengine">
                            // <Property name="dynamicloadmapping">
                            // <Value type="3">(ObjectName="BIOG_GesturesConfigDLC.RuntimeData",SeekFreePackageName="GesturesConfigDLC")</Value>

                            var entry = new Game3CoalescedValueEntry()
                            {
                                Section = @"sfxgame.sfxengine",
                                Name = @"dynamicloadmapping",
                                Type = 3
                            };

                            entry.Values.Add($"(ObjectName=\"{outfit.AvailableImage}\",SeekFreePackageName=\"SFXHenchImages_{outfit.DLCName}\")"); // do not localize
                            entry.Values.Add($"(ObjectName=\"{outfit.HighlightImage}\",SeekFreePackageName=\"SFXHenchImages_{outfit.DLCName}\")"); // do not localize
                            Game3CoalescedHelper.AddArrayEntry(bioEngineDoc, entry);
                        }
                    }

                    mergeCoal[@"BIOEngine.xml"] = bioEngineDoc.ToString();


                    CoalescedConverter.CompileFromMemory(mergeCoal).WriteToFile(mergeCoalFile);
                }
            }
        }

        private static void buildPlotElementObject(ArrayProperty<StructProperty> plotStreaming, SquadmateInfoSingle sqm, MEGame game, bool isExplore)
        {
            var fName = sqm.HenchPackage;
            var virtualChunk = $@"BioH_{sqm.HenchName}";
            if (game.IsGame3() && isExplore)
            {
                fName += @"_Explore";
                virtualChunk += @"_Explore";
            }

            var element = plotStreaming.FirstOrDefault(x => x.GetProp<NameProperty>(@"VirtualChunkName").Value == virtualChunk);
            if (element != null)
            {
                var elem = element.GetProp<ArrayProperty<StructProperty>>(@"Elements");
                sqm.MemberAppearanceValue = elem.Count;
                elem.Add(GeneratePlotStreamingElement(fName, sqm.ConditionalIndex));
            }
        }
    }
}
