public class InfOreExtractorMain : FortressCraftMod
{
	public ushort mHopperCubeType;

	public static InfExtractorMachineWindow InfExtractorUI = new InfExtractorMachineWindow();

	private void Start()
	{
		Variables.Start();
	}

	public override ModRegistrationData Register()
	{
		ModRegistrationData modRegistrationData = new ModRegistrationData();
		modRegistrationData.RegisterEntityHandler("Tricky.InfOreExtractor");
		modRegistrationData.RegisterEntityUI("Tricky.InfOreExtractor", InfOreExtractorMain.InfExtractorUI);
		UIManager.NetworkCommandFunctions.Add("InfExtractorMachineWindow", InfExtractorMachineWindow.HandleNetworkCommand);
		TerrainDataEntry terrainDataEntry = default(TerrainDataEntry);
		TerrainDataValueEntry terrainDataValueEntry = default(TerrainDataValueEntry);
		TerrainData.GetCubeByKey("Tricky.InfOreExtractor", out terrainDataEntry, out terrainDataValueEntry);
		if (terrainDataEntry != null)
		{
			this.mHopperCubeType = terrainDataEntry.CubeType;
		}
		return modRegistrationData;
	}

	public override ModCreateSegmentEntityResults CreateSegmentEntity(ModCreateSegmentEntityParameters parameters)
	{
		ModCreateSegmentEntityResults modCreateSegmentEntityResults = new ModCreateSegmentEntityResults();
		if (parameters.Cube == this.mHopperCubeType)
		{
			modCreateSegmentEntityResults.Entity = new InfOreExtractor(parameters.Segment, parameters.X, parameters.Y, parameters.Z, parameters.Cube, parameters.Flags, parameters.Value, parameters.LoadFromDisk);
		}
		return modCreateSegmentEntityResults;
	}
}
