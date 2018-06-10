using System;
using MadVandal.FortressCraft;

namespace Tricky.InfiniteOreExtractor
{
    public class InfOreExtractorMain : FortressCraftMod
    {
        private const string MOD_KEY = "Tricky.InfOreExtractor";

        private const string MOD_CUBE_KEY = "Tricky.InfOreExtractor";

        private ushort mCubeType;

        public override ModRegistrationData Register()
        {
            try
            {
                Logging.ModName = "Tricky! Infinite Ore Extractor";

                ModRegistrationData modRegistrationData = new ModRegistrationData();
                modRegistrationData.RegisterEntityHandler(MOD_KEY);
                modRegistrationData.RegisterEntityUI(MOD_KEY, new InfExtractorMachineWindow());
                UIManager.NetworkCommandFunctions.Add(InfExtractorMachineWindow.InterfaceName, InfExtractorMachineWindow.HandleNetworkCommand);
                TerrainDataEntry terrainDataEntry;
                TerrainDataValueEntry terrainDataValueEntry = default(TerrainDataValueEntry);
                TerrainData.GetCubeByKey(MOD_CUBE_KEY, out terrainDataEntry, out terrainDataValueEntry);
                if (terrainDataEntry != null)
                    mCubeType = terrainDataEntry.CubeType;
                else 
                    Logging.LogMissingCubeKey(MOD_CUBE_KEY);

                return modRegistrationData;
            }
            catch (Exception e)
            {
                Logging.LogException(e);
                return new ModRegistrationData();
            }
        }

        public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
        {
            ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();

            try
            {
                if (parameters.Cube == mCubeType)
                    modCreateSegmentEntityResults.Entity = new InfOreExtractor(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags,
                        parameters.Value, parameters.LoadFromDisk);
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }

            return modCreateSegmentEntityResults;
        }
    }
}
