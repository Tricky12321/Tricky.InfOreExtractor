using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InfOreExtractor : MachineEntity, PowerConsumerInterface, StorageSupplierInterface
{
    private enum eDrillHeadType
    {
        eDefault = 1,
        eSteel,
        eCrystal,
        eOrganic,
        ePlasma
    }

    public enum eState
    {
        eFetchingEntryPoint,
        eFetchingExtractionPoint,
        eSearchingForOre,
        eMining,
        eOutOfPower,
        eOutOfStorage,
        eVeinDepleted,
        eOutOfPowerVeinDepleted,
        eOutOfStorageVeinDepleted,
        eDrillStuck,
        eIdle
    }

    public const ushort MIN_ORE_VALUE = 1;
    public const ushort MAX_ORE_VALUE = 2560;
    public const int MAX_SEARCH_STEPS = 16384;
    public const float EXTRACTION_TIME = 30f;
    public const float POWER_TRANSFER_RATE = 60f;
    public const int MAX_DURABILITY = 10000;
    public const float CUTTER_EFFICIENCY_T1 = 0.1f;
    public const float CUTTER_EFFICIENCY_T2 = 0.2f;
    public const float CUTTER_EFFICIENCY_T3 = 0.3f;
    public const float CUTTER_EFFICIENCY_T4 = 0.5f;
    public const float CUTTER_EFFICIENCY_T5 = 4f;
    public const int CUTTER_HARDNESS_T1 = 150;
    public const int CUTTER_HARDNESS_T2 = 250;
    public const int CUTTER_HARDNESS_T3 = 250;
    public const int CUTTER_HARDNESS_T4 = 250;
    public const int CUTTER_HARDNESS_T5 = 250;
    public const int DRILLMOTOR_RATE_T0 = 1;
    public const int DRILLMOTOR_RATE_T1 = 2;
    public const int DRILLMOTOR_RATE_T2 = 4;
    public const int DRILLMOTOR_RATE_T3 = 8;
    public const int DRILLMOTOR_RATE_T4 = 16;
    public const int DRILLMOTOR_RATE_T5 = 32;
    public int mnDrillRate = 1;
    private int mnVisualCutterTier = -1;
    public int mnDrillTier;
    public int mnCutterTier = 1;
    public int mnCutterDurability;
    public int mnCutterMaxDurability;
    private float mrTargetPitch;
    public float mrMaxPower = 500f;
    public float mrCurrentPower;
    private int mnMaxOre = 25;
    public int mnStoredOre;
    public ushort mnOreType;
    public float mrTimeUntilNextOre;
    public int mnEstimatedOreLeft;
    public float mrEfficiency;
    public float mrWorkTime;
    public float mrIdleTime;
    public float mrWorkEfficiency;
    public float mrNormalisedPower;
    public float OreCollectionRate = 1f;
    private long ExtractionX;
    private long ExtractionY;
    private long ExtractionZ;
    private long EntryX;
    private long EntryY;
    private long EntryZ;
    public eState meState;
    public float mrExtractionTime = 30f;
    public float mrPowerUsage = 0.5f;
    public float mrSparePowerCapacity;
    private Queue<CubeCoord> queuedLocations;
    private HashSet<CubeCoord> visitedLocations;
    private CubeCoord searchLocation;
    private float mrReadoutTick;
    private int mnLowFrequencyUpdates;
    private int mnUpdates;
    private float mrTimeUntilFlash;
    private float mrTimeElapsed;
    private int mnDepleteCount;
    public int mnBonusOre;
    public int mnBonusDeplete;
    private bool mbRotateModel;
    private Quaternion mTargetRotation = Quaternion.identity;
    private bool mbLinkedToGO;
    public GameObject MiningSparks;
    public GameObject DrillChunks;
    public GameObject OreExtractorModel;
    private RotateConstantlyScript mRotCont;
    private GameObject DrillHeadObject;
    private Renderer OreExtractorBarThing;
    public MaterialPropertyBlock mPowerMPB;
    public TextMesh DebugReadout;
    private Light WorkLight;
    public float mrAveragePPS;
    public string mName = string.Empty;
    private int mnSign;
    private int lnCutter;
    public float mrIssueTime;
    public bool mbReportOffline = true;
    private float base_efficiency;
    private GameObject TutorialEffect;
    private GameObject LowPowerTutorial;
    private GameObject FullStorageTutorial;
    public static int STEEL_HEAD_ID = 200;
    public static int CRYSTAL_HEAD_ID = 201;
    public static int ORGANIC_HEAD_ID = 202;
    public static int PLASMA_HEAD_ID = 203;

    private Color mCubeColor;
    private int CubeValue;
    private string MachineName;
    private int TierUpgrade = 0;

    public InfOreExtractor(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool wasLoaded) : base(eSegmentEntity.Mod, SpawnableObjectEnum.OreExtractor, x, y, z, cube, flags, lValue, Vector3.zero, segment)
    {
        base.mbNeedsLowFrequencyUpdate = true;
        base.mbNeedsUnityUpdate = true;
        this.mrTimeUntilNextOre = this.mrExtractionTime;
        this.searchLocation = CubeCoord.Invalid;
        this.mnOreType = 0;
        string cubeKey = TerrainData.GetCubeKey(cube, lValue);
        if (cubeKey == "Tricky.StockInfOreExtractor")
        {
            this.TierUpgrade = 0;
            this.MachineName = "Stock Infinte Ore Extractor";
            this.mCubeColor = new Color(2f, 0.5f, 0.5f);
            this.CubeValue = 0;
        }
        if (cubeKey == "Tricky.AdvancedInfOreExtractor")
        {
            this.TierUpgrade = 1;
            this.MachineName = "Advanced Infinte Ore Extractor";
            this.mCubeColor = new Color(0.5f, 0.5f, 4.5f);
            this.CubeValue = 1;
        }
        if (!wasLoaded)
        {
            this.SetNewState(eState.eFetchingEntryPoint);
        }
        else
        {
            this.SetNewState(eState.eFetchingExtractionPoint);
        }
        this.mnCutterTier = 1;
        if (!segment.mbValidateOnly)
        {
            this.CalculateEfficiency();
            this.SetTieredUpgrade(0);
        }
    }

    public override void CleanUp()
    {
        base.CleanUp();
        this.queuedLocations = null;
        this.visitedLocations = null;
    }

    public override void LowFrequencyUpdate()
    {
        float num = this.mrCurrentPower;
        this.UpdatePlayerDistanceInfo();
        this.mnLowFrequencyUpdates++;
        GameManager.mnNumOreExtractors_Transitory++;
        this.mrNormalisedPower = this.mrCurrentPower / this.mrMaxPower;
        this.mrSparePowerCapacity = this.mrMaxPower - this.mrCurrentPower;
        this.mrReadoutTick -= LowFrequencyThread.mrPreviousUpdateTimeStep;
        if (this.mrReadoutTick < 0f)
        {
            this.mrReadoutTick = 5f;
        }
        this.UpdateState();
        float num2 = num - this.mrCurrentPower;
        num2 /= LowFrequencyThread.mrPreviousUpdateTimeStep;
        this.mrAveragePPS += (num2 - this.mrAveragePPS) / 8f;
        if (this.mnCutterTier == 1)
        {
            this.LookForCutterHead();
        }
        this.LookForSign();
    }

    private bool AttemptAutoUpgrade(int lnID, StorageMachineInterface storageHopper)
    {
        ItemBase itemBase = default(ItemBase);
        if (storageHopper.TryExtractItems((StorageUserInterface)this, lnID, 1, out itemBase))
        {
            ItemDurability itemDurability = itemBase as ItemDurability;
            if (itemDurability != null)
            {
                Achievements.UnlockAchievementDelayed(Achievements.eAchievements.eOpinionsareLike);
                this.SetCutterUpgrade(itemDurability);
                return true;
            }
        }
        return false;
    }

    private void LookForSign()
    {
        if (!((Object)FloatingCombatTextManager.instance == (Object)null))
        {
            this.mnSign++;
            long num = base.mnX;
            long num2 = base.mnY;
            long num3 = base.mnZ;
            if (this.mnSign % 6 == 0)
            {
                num--;
            }
            if (this.mnSign % 6 == 1)
            {
                num++;
            }
            if (this.mnSign % 6 == 2)
            {
                num2--;
            }
            if (this.mnSign % 6 == 3)
            {
                num2++;
            }
            if (this.mnSign % 6 == 4)
            {
                num3--;
            }
            if (this.mnSign % 6 == 5)
            {
                num3++;
            }
            Segment segment = base.AttemptGetSegment(num, num2, num3);
            if (segment != null && segment.IsSegmentInAGoodState())
            {
                Sign sign = segment.SearchEntity(num, num2, num3) as Sign;
                if (sign != null)
                {
                    if (!(this.mName == string.Empty) && !(this.mName != sign.mText))
                    {
                        return;
                    }
                    this.mName = sign.mText;
                    FloatingCombatTextManager.instance.QueueText(base.mnX, base.mnY, base.mnZ, 0.75f, this.mName, Color.green, 1f, 16f);
                }
            }
        }
    }

    private void LookForCutterHead()
    {
        if (WorldScript.mbIsServer)
        {
            this.lnCutter++;
            long num = base.mnX;
            long num2 = base.mnY;
            long num3 = base.mnZ;
            if (this.lnCutter % 6 == 0)
            {
                num--;
            }
            if (this.lnCutter % 6 == 1)
            {
                num++;
            }
            if (this.lnCutter % 6 == 2)
            {
                num2--;
            }
            if (this.lnCutter % 6 == 3)
            {
                num2++;
            }
            if (this.lnCutter % 6 == 4)
            {
                num3--;
            }
            if (this.lnCutter % 6 == 5)
            {
                num3++;
            }
            Segment segment = base.AttemptGetSegment(num, num2, num3);
            if (segment != null && segment.IsSegmentInAGoodState())
            {
                StorageMachineInterface storageMachineInterface = segment.SearchEntity(num, num2, num3) as StorageMachineInterface;
                if (storageMachineInterface != null)
                {
                    eHopperPermissions permissions = storageMachineInterface.GetPermissions();
                    if (permissions != 0 && permissions != eHopperPermissions.RemoveOnly)
                    {
                        return;
                    }
                    if (this.AttemptAutoUpgrade(OreExtractor.PLASMA_HEAD_ID, storageMachineInterface) || (!this.AttemptAutoUpgrade(OreExtractor.ORGANIC_HEAD_ID, storageMachineInterface) && (this.AttemptAutoUpgrade(OreExtractor.CRYSTAL_HEAD_ID, storageMachineInterface) || !this.AttemptAutoUpgrade(OreExtractor.STEEL_HEAD_ID, storageMachineInterface))))
                    {
                        ;
                    }
                }
            }
        }
    }

    public void UpdateState()
    {
        if (this.meState == eState.eOutOfStorage)
        {
            if (this.mnStoredOre + this.mnDrillRate + this.mnBonusOre < this.mnMaxOre)
            {
                this.SetNewState(eState.eMining);
            }
            this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
        }
        else if (this.meState == eState.eOutOfStorageVeinDepleted)
        {
            if (this.mnStoredOre + this.mnDrillRate + this.mnBonusOre < this.mnMaxOre)
            {
                this.SetNewState(eState.eVeinDepleted);
            }
            else
            {
                this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
        }
        if (this.meState != eState.eDrillStuck)
        {

        }
        if (this.meState == eState.eFetchingEntryPoint)
        {
            this.UpdateFetchEntryPoint();
        }
        if (this.meState == eState.eFetchingExtractionPoint)
        {
            this.UpdateFetchExtractionPoint();
        }
        if (this.meState == eState.eSearchingForOre)
        {
            this.DoVeinSearch();
        }
        if (this.meState == eState.eMining)
        {
            this.UpdateMining();
            this.mrIssueTime = 0f;
            this.mrWorkTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
        }
        else
        {
            this.mrIdleTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
        }
        float num = this.mrWorkTime + this.mrIdleTime;
        this.mrWorkEfficiency = this.mrWorkTime / num;
        if (this.meState == eState.eVeinDepleted)
        {
            this.UpdateVeinDepleted();
            this.mrIssueTime = 0f;
        }
        if (this.meState == eState.eOutOfPower)
        {
            this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            if (this.mrCurrentPower > this.mrPowerUsage)
            {
                this.SetNewState(eState.eMining);
            }
        }
        if (this.meState == eState.eOutOfPowerVeinDepleted)
        {
            this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            if (this.mrCurrentPower > this.mrPowerUsage)
            {
                this.SetNewState(eState.eVeinDepleted);
            }
        }
        if (this.meState == eState.eIdle)
        {
            this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            if (base.mFrustrum != null)
            {
                base.DropMachineFrustrum();
            }
        }
        if (this.meState == eState.eDrillStuck)
        {
            this.mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
        }
        if (this.mrIssueTime > 300f && this.mbReportOffline)
        {
            if (this.mrIssueTime > ARTHERPetSurvival.OreErrorTime && this.mnOreType != 0)
            {
                ARTHERPetSurvival.OreErrorTime = this.mrIssueTime;
                ARTHERPetSurvival.OreErrorType = this.mnOreType;
                ARTHERPetSurvival.ExtractorName = this.mName;
            }
            GameManager.mnNumOreExtractors_With_Issues_Transitory++;
        }
    }

    public void UpdateFetchEntryPoint()
    {
        this.EntryX = (this.EntryY = (this.EntryZ = 0L));
        Vector3 directionVector = CubeHelper.GetDirectionVector((byte)(base.mFlags & 0x3F));
        if (this.CheckNeighbourEntryCube(directionVector))
        {
            if (this.EntryX != 0)
            {
                this.RotateExtractorTo(directionVector);
            }
        }
        else
        {
            Vector3 forward = SegmentCustomRenderer.GetRotationQuaternion(base.mFlags) * Vector3.forward;
            forward.Normalize();
            if (!this.CheckNeighbourEntryCube(forward))
            {
                if (this.CheckNeighbourEntryCube(Vector3.forward))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.forward);
                    }
                }
                else if (this.CheckNeighbourEntryCube(Vector3.back))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.back);
                    }
                }
                else if (this.CheckNeighbourEntryCube(Vector3.left))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.left);
                    }
                }
                else if (this.CheckNeighbourEntryCube(Vector3.right))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.right);
                    }
                }
                else if (this.CheckNeighbourEntryCube(Vector3.down))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.down);
                    }
                }
                else if (this.CheckNeighbourEntryCube(Vector3.up))
                {
                    if (this.EntryX != 0)
                    {
                        this.RotateExtractorTo(Vector3.up);
                    }
                }
                else
                {
                    this.EntryX = (this.EntryY = (this.EntryZ = 0L));
                    this.ExtractionX = (this.ExtractionY = (this.ExtractionZ = 0L));
                    this.SetNewState(eState.eIdle);
                }
            }
        }
    }

    private bool CheckNeighbourEntryCube(Vector3 forward)
    {
        long num = base.mnX + (long)forward.x;
        long num2 = base.mnY + (long)forward.y;
        long num3 = base.mnZ + (long)forward.z;
        ushort cube = WorldScript.instance.GetCube(num, num2, num3);
        if (cube == 0)
        {
            if (WorldScript.mbIsServer)
            {
                SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(base.mFrustrum, num, num2, num3);
            }
            return true;
        }
        if (CubeHelper.IsOre(cube))
        {
            this.EntryX = num;
            this.EntryY = num2;
            this.EntryZ = num3;
            this.mnOreType = cube;
            ARTHERPetSurvival.instance.GotOre(this.mnOreType);
            this.SetNewState(eState.eSearchingForOre);
            return true;
        }
        return false;
    }

    private void RotateExtractorTo(Vector3 direction)
    {
        byte b = 1;
        if (direction.y > 0.5f)
        {
            b = 132;
        }
        else if (direction.y < -0.5f)
        {
            b = 4;
        }
        else
        {
            int num = 0;
            if (direction.x < -0.5f)
            {
                num = 1;
            }
            else if (direction.z < -0.5f)
            {
                num = 2;
            }
            if (direction.x > 0.5f)
            {
                num = 3;
            }
            b = (byte)(1 | num << 6);
        }
        if (b != base.mFlags)
        {
            base.mFlags = b;
            base.mSegment.SetFlagsNoChecking((int)(base.mnX % 16), (int)(base.mnY % 16), (int)(base.mnZ % 16), base.mFlags);
            base.mSegment.RequestDelayedSave();
            if (base.mWrapper != null && base.mWrapper.mbHasGameObject)
            {
                this.mTargetRotation = SegmentCustomRenderer.GetRotationQuaternion(b);
                this.mbRotateModel = true;
            }
        }
    }

    public void UpdateFetchExtractionPoint()
    {
        ushort num = 0;
        if (base.mFrustrum != null)
        {
            Segment segment = base.mFrustrum.GetSegment(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
            if (segment == null)
            {
                SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(base.mFrustrum, this.ExtractionX, this.ExtractionY, this.ExtractionZ);
                return;
            }
            if (!segment.mbInitialGenerationComplete)
            {
                return;
            }
            if (segment.mbDestroyed)
            {
                return;
            }
            num = segment.GetCube(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
        }
        else
        {
            num = WorldScript.instance.GetCube(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
            if (num == 0)
            {
                return;
            }
        }
        if (num == this.mnOreType)
        {
            ARTHERPetSurvival.instance.GotOre(this.mnOreType);
            if (this.mnStoredOre >= this.mnMaxOre)
            {
                this.SetNewState(eState.eOutOfStorage);
            }
            else
            {
                this.SetNewState(eState.eMining);
            }
        }
        else
        {
            this.SetNewState(eState.eSearchingForOre);
        }
    }

    private void UpdateMining()
    {
        if (this.ExtractionX == 0)
        {
            this.SetNewState(eState.eSearchingForOre);
        }
        else if (!this.CheckHardness())
        {
            this.SetNewState(eState.eDrillStuck);
        }
        else
        {
            this.mrPowerUsage = 0.5f;
            if (DifficultySettings.mbEasyPower)
            {
                this.mrPowerUsage *= 0.5f;
            }
            if (this.mnDrillTier == 0)
            {
                this.mrPowerUsage *= 0.25f;
            }
            this.mrPowerUsage += (float)(this.mnDrillRate - 1) / 2f;
            float num = this.mrPowerUsage * LowFrequencyThread.mrPreviousUpdateTimeStep * DifficultySettings.mrResourcesFactor;
            if (num <= this.mrCurrentPower)
            {
                this.mrCurrentPower -= num;
                this.mrTimeUntilNextOre -= LowFrequencyThread.mrPreviousUpdateTimeStep * this.OreCollectionRate;
                if (this.mrTimeUntilNextOre <= 0f)
                {
                    ushort num2 = 0;
                    Segment segment = null;
                    segment = ((base.mFrustrum == null) ? WorldScript.instance.GetSegment(this.ExtractionX, this.ExtractionY, this.ExtractionZ) : base.mFrustrum.GetSegment(this.ExtractionX, this.ExtractionY, this.ExtractionZ));
                    if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                    {
                        this.mrTimeUntilNextOre = this.mrExtractionTime;
                        this.SetNewState(eState.eFetchingExtractionPoint);
                    }
                    else
                    {
                        ushort cube = segment.GetCube(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
                        if (cube != this.mnOreType)
                        {
                            this.mrTimeUntilNextOre = this.mrExtractionTime;
                            this.SetNewState(eState.eFetchingEntryPoint);
                        }
                        else
                        {
                            CubeData cubeData = segment.GetCubeData(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
                            num2 = cubeData.mValue;
                            int num3 = this.mnDepleteCount + this.mnBonusDeplete;
                            num3 = (int)((float)num3 / this.OreCollectionRate);
                            if (num3 >= num2)
                            {
                                Debug.LogWarning("Ore Extractor doing final Mining extraction before moving to Clearing on next update, due to wanting to remove " + num3 + " ore when there was only " + num2 + " ore left!");
                                num3 = num2 - 1;
                            }
                            if (num2 > 1)
                            {
                                ushort num4 = num2;
                                if (num3 >= 2560)
                                {
                                    Debug.LogError("Error, risking overflow - deplete count is " + num3 + " and max ore is " + (ushort)2560);
                                }
                                if (num2 > 2560)
                                {
                                    num2 = 2560;
                                }
                                num2 = (ushort)(this.mnEstimatedOreLeft = (ushort)(num2 - (ushort)num3));
                                if (num2 > num4)
                                {
                                    Debug.LogError("Error! We just removed " + num3 + " ore, but now we have " + num2 + " ore left!");
                                }
                                segment.SetCubeValueNoChecking((int)(this.ExtractionX % 16), (int)(this.ExtractionY % 16), (int)(this.ExtractionZ % 16), num2);
                                segment.RequestDelayedSave();
                                if (TerrainData.GetSideTexture(this.mnOreType, num2) != TerrainData.GetSideTexture(this.mnOreType, num4))
                                {
                                    segment.RequestRegenerateGraphics();
                                }
                                int num5 = this.mnDrillRate + this.mnBonusOre;
                                num5 = (int)((float)num5 / this.OreCollectionRate);
                                this.mnStoredOre += num5;
                                GameManager.AddOre(num5);
                                this.LowerCutterDurability(num5);
                                this.RequestImmediateNetworkUpdate();
                                this.MarkDirtyDelayed();
                                this.mrTimeUntilNextOre = this.mrExtractionTime;
                                if (this.mnStoredOre >= this.mnMaxOre)
                                {
                                    this.SetNewState(eState.eOutOfStorage);
                                }
                            }
                            else if (this.EntryX != 0)
                            {
                                this.SetNewState(eState.eSearchingForOre);
                            }
                            else
                            {
                                this.SetNewState(eState.eFetchingEntryPoint);
                            }
                        }
                    }
                }
            }
            else
            {
                this.SetNewState(eState.eOutOfPower);
            }
        }
    }

    private void LowerCutterDurability(int lnOreCount)
    {
        if (this.mnCutterTier > 1)
        {
            this.mnCutterDurability -= lnOreCount;
            if (this.mnCutterDurability <= 0)
            {
                this.mnCutterTier = 1;
                this.mnCutterDurability = 10000;
                this.CalculateEfficiency();
            }
        }
    }

    private void DoVeinSearch()
    {
        if (this.EntryX == 0)
        {
            this.SetNewState(eState.eFetchingEntryPoint);
        }
        else
        {
            this.ExtractionX = (this.ExtractionY = (this.ExtractionZ = 0L));
            if (this.searchLocation == CubeCoord.Invalid)
            {
                this.queuedLocations = new Queue<CubeCoord>(100);
                this.visitedLocations = new HashSet<CubeCoord>();
                if (!this.SearchNeighbourLocation(this.EntryX, this.EntryY, this.EntryZ))
                {
                    return;
                }
            }
            else if (!this.SearchNeighbours())
            {
                return;
            }
            while (this.queuedLocations.Count > 0)
            {
                this.searchLocation = this.queuedLocations.Dequeue();
                this.visitedLocations.Add(this.searchLocation);
                if (!this.SearchNeighbours())
                {
                    return;
                }
            }
            if (this.ExtractionX != 0)
            {
                if (this.mnStoredOre >= this.mnMaxOre)
                {
                    this.SetNewState(eState.eOutOfStorage);
                }
                else
                {
                    this.SetNewState(eState.eMining);
                }
                this.queuedLocations = null;
                this.visitedLocations = null;
                if (base.mFrustrum != null)
                {
                    Segment segment = base.mFrustrum.GetSegment(this.ExtractionX, this.ExtractionY, this.ExtractionZ);
                    for (int i = 0; i < base.mFrustrum.mSegments.Count; i++)
                    {
                        Segment segment2 = base.mFrustrum.mSegments[i];
                        if (segment2 != null && segment2 != base.mFrustrum.mMainSegment && segment2 != segment)
                        {
                            SegmentManagerThread.instance.RequestSegmentDropForMachineFrustrum(base.mFrustrum, segment2);
                        }
                    }
                }
            }
            else
            {
                this.queuedLocations = null;
                if (this.mnStoredOre >= this.mnMaxOre)
                {
                    this.SetNewState(eState.eOutOfStorageVeinDepleted);
                }
                else
                {
                    this.SetNewState(eState.eVeinDepleted);
                }
            }
            this.searchLocation = CubeCoord.Invalid;
        }
    }

    private bool SearchNeighbours()
    {
        if (!this.SearchNeighbourLocation(this.searchLocation.x + 1, this.searchLocation.y, this.searchLocation.z))
        {
            return false;
        }
        if (this.ExtractionX != 0)
        {
            return true;
        }
        if (!this.SearchNeighbourLocation(this.searchLocation.x - 1, this.searchLocation.y, this.searchLocation.z))
        {
            return false;
        }
        if (this.ExtractionX != 0)
        {
            return true;
        }
        if (!this.SearchNeighbourLocation(this.searchLocation.x, this.searchLocation.y + 1, this.searchLocation.z))
        {
            return false;
        }
        if (this.ExtractionX != 0)
        {
            return true;
        }
        if (!this.SearchNeighbourLocation(this.searchLocation.x, this.searchLocation.y - 1, this.searchLocation.z))
        {
            return false;
        }
        if (this.ExtractionX != 0)
        {
            return true;
        }
        if (!this.SearchNeighbourLocation(this.searchLocation.x, this.searchLocation.y, this.searchLocation.z + 1))
        {
            return false;
        }
        if (this.ExtractionX != 0)
        {
            return true;
        }
        if (!this.SearchNeighbourLocation(this.searchLocation.x, this.searchLocation.y, this.searchLocation.z - 1))
        {
            return false;
        }
        return true;
    }

    private bool SearchNeighbourLocation(long x, long y, long z)
    {
        Segment segment = null;
        if (base.mFrustrum != null)
        {
            segment = base.mFrustrum.GetSegment(x, y, z);
            if (segment == null)
            {
                SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(base.mFrustrum, x, y, z);
                return false;
            }
            if (segment.mbInitialGenerationComplete && !segment.mbDestroyed)
            {
                goto IL_0084;
            }
            return false;
        }
        segment = WorldScript.instance.GetSegment(x, y, z);
        if (segment != null && segment.mbInitialGenerationComplete && !segment.mbDestroyed)
        {
            goto IL_0084;
        }
        return false;
        IL_0084:
        ushort cube = segment.GetCube(x, y, z);
        if (cube == this.mnOreType)
        {
            CubeCoord item = new CubeCoord(x, y, z);
            if (this.visitedLocations == null)
            {
                Debug.LogError("Error! visitedLocations is null!");
                return false;
            }
            if (this.queuedLocations == null)
            {
                Debug.LogError("Error! queuedLocations is null!");
                return false;
            }
            if (!this.visitedLocations.Contains(item) && !this.queuedLocations.Contains(item))
            {
                CubeData cubeDataNoChecking = segment.GetCubeDataNoChecking((int)(x % 16), (int)(y % 16), (int)(z % 16));
                ushort mValue = cubeDataNoChecking.mValue;
                if (mValue > this.mnDepleteCount + this.mnBonusDeplete)
                {
                    this.ExtractionX = x;
                    this.ExtractionY = y;
                    this.ExtractionZ = z;
                    this.queuedLocations.Clear();
                }
                else if (this.visitedLocations.Count < 16384)
                {
                    this.queuedLocations.Enqueue(item);
                }
            }
        }
        return true;
    }

    private void UpdateVeinDepleted()
    {
        float num = this.mrPowerUsage * LowFrequencyThread.mrPreviousUpdateTimeStep * DifficultySettings.mrResourcesFactor;
        if (num <= this.mrCurrentPower)
        {
            this.mrCurrentPower -= num;
            this.mrTimeUntilNextOre -= LowFrequencyThread.mrPreviousUpdateTimeStep * this.OreCollectionRate;
            if (!this.CheckHardness())
            {
                this.queuedLocations = null;
                this.visitedLocations = null;
                this.SetNewState(eState.eDrillStuck);
            }
            else if (this.mrTimeUntilNextOre <= 0f)
            {
                if (this.visitedLocations == null)
                {
                    this.queuedLocations = null;
                    this.visitedLocations = null;
                    this.SetNewState(eState.eIdle);
                    Debug.Log("Ore Extractor cleared vein, going to idle mode");
                }
                else
                {
                    bool flag = false;
                    int num2 = -1;
                    long num3 = 0L;
                    long y = 0L;
                    long z = 0L;
                    Segment segment = null;
                    while (this.visitedLocations.Count > 0 && !flag)
                    {
                        num2 = -1;
                        num3 = 0L;
                        y = 0L;
                        z = 0L;
                        foreach (CubeCoord visitedLocation in this.visitedLocations)
                        {
                            CubeCoord current = visitedLocation;
                            long val = current.x - this.EntryX;
                            long val2 = current.y - this.EntryY;
                            long val3 = current.z - this.EntryZ;
                            int num4 = (int)Util.Abs(val) + (int)Util.Abs(val2) + (int)Util.Abs(val3);
                            if (num4 > num2)
                            {
                                num2 = num4;
                                num3 = current.x;
                                y = current.y;
                                z = current.z;
                            }
                        }
                        if (num3 == 0)
                        {
                            throw new AssertException("Ore Extractor: vein depleted visitedlocations broken");
                        }
                        segment = ((base.mFrustrum == null) ? WorldScript.instance.GetSegment(num3, y, z) : base.mFrustrum.GetSegment(num3, y, z));
                        if (segment != null && segment.mbInitialGenerationComplete && !segment.mbDestroyed)
                        {
                            ushort cube = segment.GetCube(num3, y, z);
                            if (cube != this.mnOreType)
                            {
                                this.visitedLocations.Remove(new CubeCoord(num3, y, z));
                            }
                            else
                            {
                                flag = true;
                            }
                            continue;
                        }
                        this.queuedLocations = null;
                        this.visitedLocations = null;
                        this.SetNewState(eState.eFetchingEntryPoint);
                        return;
                    }
                    if (flag)
                    {
                        CubeData cubeData = segment.GetCubeData(num3, y, z);
                        ushort mValue = cubeData.mValue;
                        int num5 = (int)Mathf.Ceil((float)(int)mValue * this.mrEfficiency);
                        this.LowerCutterDurability(num5);
                        this.mnStoredOre += num5;
                        GameManager.AddOre(num5);
                        WorldScript.instance.BuildFromEntity(segment, num3, y, z, 1, 0);
                        this.MarkDirtyDelayed();
                        this.mrTimeUntilNextOre = this.mrExtractionTime;
                        if (this.mnStoredOre >= this.mnMaxOre)
                        {
                            this.SetNewState(eState.eOutOfStorageVeinDepleted);
                            return;
                        }
                        if (this.visitedLocations == null)
                        {
                            Debug.LogError("visitedLocations is null!");
                        }
                        else
                        {
                            this.visitedLocations.Remove(new CubeCoord(num3, y, z));
                        }
                        if (base.mDistanceToPlayer < 128f && base.mDotWithPlayerForwards > 0f)
                        {
                            FloatingCombatTextManager.instance.QueueText(base.mnX, base.mnY + 1, base.mnZ, 0.75f, "Clearing!", Color.green, 1f, 64f);
                        }
                    }
                    if (this.visitedLocations.Count == 0)
                    {
                        this.queuedLocations = null;
                        this.visitedLocations = null;
                        this.SetNewState(eState.eIdle);
                        Debug.Log("Ore Extractor cleared vein, going to idle mode");
                    }
                }
            }
        }
        else
        {
            this.queuedLocations = null;
            this.SetNewState(eState.eOutOfPowerVeinDepleted);
        }
    }

    private void CalculateEfficiency()
    {
        this.base_efficiency = 0.1f;
        switch (this.mnCutterTier)
        {
            case 2:
                this.base_efficiency = 0.2f;
                break;
            case 3:
                this.base_efficiency = 0.3f;
                break;
            case 4:
                this.base_efficiency = 0.5f;
                break;
            case 5:
                this.base_efficiency = 4f;
                break;
        }
        this.mrEfficiency = this.base_efficiency * (100f / TerrainData.GetHardness(this.mnOreType, 0));
        if (this.mrEfficiency > 1f)
        {
            this.mrEfficiency = 1f;
        }
        this.CalcEfficiencyAndDepleteRate();
    }

    private bool CheckHardness()
    {
        int num = 150;
        switch (this.mnCutterTier)
        {
            case 2:
                num = 250;
                break;
            case 3:
                num = 250;
                break;
            case 4:
                num = 250;
                break;
            case 5:
                num = 250;
                break;
        }
        return TerrainData.GetHardness(this.mnOreType, 0) <= (float)num;
    }

    private void SetNewState(eState leNewState)
    {
        if (leNewState != this.meState)
        {
            if (leNewState == eState.eVeinDepleted && this.meState != eState.eOutOfStorageVeinDepleted && this.meState != eState.eOutOfPowerVeinDepleted)
            {
                ARTHERPetSurvival.ClearingType = this.mnOreType;
                ARTHERPetSurvival.ExtractorEnteringClearing = true;
                ARTHERPetSurvival.ExtractorName = this.mName;
            }
            this.RequestImmediateNetworkUpdate();
            this.meState = leNewState;
        }
    }

    public override void DropGameObject()
    {
        base.DropGameObject();
        this.mbLinkedToGO = false;
        if ((Object)this.TutorialEffect != (Object)null)
        {
            Object.Destroy(this.TutorialEffect);
            this.TutorialEffect = null;
        }
        if ((Object)this.FullStorageTutorial != (Object)null)
        {
            Object.Destroy(this.FullStorageTutorial);
            this.FullStorageTutorial = null;
        }
        if ((Object)this.LowPowerTutorial != (Object)null)
        {
            Object.Destroy(this.LowPowerTutorial);
            this.LowPowerTutorial = null;
        }
    }

    public override void UnitySuspended()
    {
        this.WorkLight = null;
        this.MiningSparks = null;
        this.DrillChunks = null;
        this.mRotCont = null;
        this.DebugReadout = null;
        this.DrillHeadObject = null;
        this.OreExtractorBarThing = null;
        if ((Object)this.TutorialEffect != (Object)null)
        {
            Object.Destroy(this.TutorialEffect);
            this.TutorialEffect = null;
        }
        if ((Object)this.FullStorageTutorial != (Object)null)
        {
            Object.Destroy(this.FullStorageTutorial);
            this.FullStorageTutorial = null;
        }
        if ((Object)this.LowPowerTutorial != (Object)null)
        {
            Object.Destroy(this.LowPowerTutorial);
            this.LowPowerTutorial = null;
        }
    }

    private void LinkToGO()
    {
        if (base.mWrapper != null && base.mWrapper.mbHasGameObject)
        {
            if (base.mWrapper.mGameObjectList == null)
            {
                Debug.LogError("Ore Extractor missing game object #0?");
            }
            if ((Object)base.mWrapper.mGameObjectList[0].gameObject == (Object)null)
            {
                Debug.LogError("Ore Extractor missing game object #0 (GO)?");
            }
            this.WorkLight = base.mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<Light>();
            if ((Object)this.WorkLight == (Object)null)
            {
                Debug.LogError("Ore extractor has missing light?");
            }
            this.WorkLight.enabled = false;
            this.MiningSparks = base.mWrapper.mGameObjectList[0].gameObject.transform.Search("MiningSparks").gameObject;
            this.DrillChunks = base.mWrapper.mGameObjectList[0].gameObject.transform.Search("DrillParticles").gameObject;
            this.OreExtractorModel = base.mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor").gameObject;
            if ((Object)this.MiningSparks == (Object)null)
            {
                Debug.LogError("Failed to find MiningSparks!");
            }
            this.DrillChunks.SetActive(false);
            this.MiningSparks.SetActive(false);
            this.mRotCont = base.mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<RotateConstantlyScript>();
            if ((Object)this.mRotCont == (Object)null)
            {
                Debug.LogError("Ore Extractor has missing rotator?");
            }
            this.DrillHeadObject = base.mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor Drills").gameObject;
            if ((Object)this.DrillHeadObject == (Object)null)
            {
                Debug.LogError("Ore Extractor Drills missing!");
            }
            this.OreExtractorBarThing = base.mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor Bar Thing").gameObject.GetComponent<Renderer>();
            if ((Object)this.OreExtractorBarThing == (Object)null)
            {
                Debug.LogError("OreExtractorBarThing missing!");
            }
            this.mPowerMPB = new MaterialPropertyBlock();
            this.mbLinkedToGO = true;
            this.DebugReadout = base.mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<TextMesh>();
            this.DebugReadout.text = "Intialising...";
            this.mrTimeUntilFlash = (float)Random.Range(0, 100) / 100f;
            this.mnVisualCutterTier = -1;
            MeshRenderer component = this.OreExtractorModel.GetComponent<MeshRenderer>();
            component.material.SetColor("_Color", this.mCubeColor);
        }
    }

    private void UpdateTutorial()
    {
        if (WorldScript.meGameMode == eGameMode.eSurvival)
        {
            if (SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.NowFuckOff)
            {
                if (this.meState == eState.eOutOfPower)
                {
                    if ((Object)this.LowPowerTutorial == (Object)null && MobSpawnManager.mrPreviousBaseThreat < 1f)
                    {
                        this.LowPowerTutorial = Object.Instantiate(SurvivalSpawns.instance.OE_NoPower, base.mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                        this.LowPowerTutorial.SetActive(true);
                        this.LowPowerTutorial.transform.parent = base.mWrapper.mGameObjectList[0].gameObject.transform;
                    }
                }
                else if ((Object)this.LowPowerTutorial != (Object)null)
                {
                    Object.Destroy(this.LowPowerTutorial);
                    this.LowPowerTutorial = null;
                }
                if (this.meState == eState.eOutOfStorage && MobSpawnManager.mrPreviousBaseThreat < 1f)
                {
                    if ((Object)this.FullStorageTutorial == (Object)null)
                    {
                        this.FullStorageTutorial = Object.Instantiate(SurvivalSpawns.instance.OE_NoStorage, base.mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                        this.FullStorageTutorial.SetActive(true);
                        this.FullStorageTutorial.transform.parent = base.mWrapper.mGameObjectList[0].gameObject.transform;
                    }
                }
                else if ((Object)this.FullStorageTutorial != (Object)null)
                {
                    Object.Destroy(this.FullStorageTutorial);
                    this.FullStorageTutorial = null;
                }
            }
            if (SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.PutPowerIntoExtractor)
            {
                if ((Object)this.TutorialEffect == (Object)null)
                {
                    this.TutorialEffect = Object.Instantiate(SurvivalSpawns.instance.PowerOE, base.mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                    this.TutorialEffect.SetActive(true);
                    this.TutorialEffect.transform.parent = base.mWrapper.mGameObjectList[0].gameObject.transform;
                }
            }
            else if ((Object)this.TutorialEffect != (Object)null)
            {
                Object.Destroy(this.TutorialEffect);
                this.TutorialEffect = null;
            }
            if (SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.RemoveCoalFromHopper && this.mnStoredOre == 0)
            {
                this.mrTimeUntilNextOre *= 0.5f;
            }
        }
    }

    public override void UnityUpdate()
    {
        this.mrTimeElapsed += Time.deltaTime;
        if (!this.mbLinkedToGO)
        {
            this.LinkToGO();
        }
        else
        {
            this.UpdateTutorial();
            if (this.mnVisualCutterTier != this.mnCutterTier)
            {
                this.mnVisualCutterTier = this.mnCutterTier;
                this.DrillHeadObject.transform.Search("Drillhead Default").gameObject.SetActive(false);
                this.DrillHeadObject.transform.Search("Drillhead Steel").gameObject.SetActive(false);
                this.DrillHeadObject.transform.Search("Drillhead Crystal").gameObject.SetActive(false);
                this.DrillHeadObject.transform.Search("Drillhead Organic").gameObject.SetActive(false);
                this.DrillHeadObject.transform.Search("Drillhead Plasma").gameObject.SetActive(false);
                if (this.mnVisualCutterTier == 1)
                {
                    this.DrillHeadObject.transform.Search("Drillhead Default").gameObject.SetActive(true);
                }
                if (this.mnVisualCutterTier == 2)
                {
                    this.DrillHeadObject.transform.Search("Drillhead Steel").gameObject.SetActive(true);
                }
                if (this.mnVisualCutterTier == 3)
                {
                    this.DrillHeadObject.transform.Search("Drillhead Crystal").gameObject.SetActive(true);
                }
                if (this.mnVisualCutterTier == 4)
                {
                    this.DrillHeadObject.transform.Search("Drillhead Organic").gameObject.SetActive(true);
                }
                if (this.mnVisualCutterTier == 5)
                {
                    this.DrillHeadObject.transform.Search("Drillhead Plasma").gameObject.SetActive(true);
                }
            }
            if (base.mDistanceToPlayer < 16f)
            {
                this.DrillHeadObject.SetActive(true);
                this.mRotCont.ZRot = base.mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch * (float)this.mnDrillRate * 0.125f;
            }
            else
            {
                this.DrillHeadObject.SetActive(false);
                this.mRotCont.ZRot = 0f;
            }
            if (this.mbRotateModel)
            {
                base.mWrapper.mGameObjectList[0].transform.rotation = this.mTargetRotation;
                this.mbRotateModel = false;
            }
            if (this.meState == eState.eMining)
            {
                if (base.mDistanceToPlayer < 32f)
                {
                    if (this.mrTargetPitch == 0f)
                    {
                        this.mrTargetPitch = Random.Range(0.95f, 1.05f);
                    }
                    if (base.mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch < this.mrTargetPitch)
                    {
                        base.mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch += Time.deltaTime * 0.1f;
                    }
                }
            }
            else
            {
                base.mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch *= 0.99f;
            }
            bool flag = true;
            if (base.mDistanceToPlayer > 128f)
            {
                flag = false;
            }
            if (base.mSegment.mbOutOfView)
            {
                flag = false;
            }
            if (base.mbWellBehindPlayer)
            {
                flag = false;
            }
            if (flag != ((Component)this.DebugReadout).GetComponent<Renderer>().enabled)
            {
                ((Component)this.DebugReadout).GetComponent<Renderer>().enabled = flag;
            }
            if (flag != this.OreExtractorModel.GetComponent<Renderer>().enabled)
            {
                this.OreExtractorModel.GetComponent<Renderer>().enabled = flag;
            }
            if (flag)
            {
                this.UpdateTextMesh();
                this.mPowerMPB.SetFloat("_GlowMult", this.mrNormalisedPower * this.mrNormalisedPower * 16f);
                this.OreExtractorBarThing.SetPropertyBlock(this.mPowerMPB);
            }
            this.mnUpdates++;
            this.UpdateWorkLight();
        }
    }

    private void UpdateTextMesh()
    {
        if (base.mDistanceToPlayer < 64f)
        {
            string text = this.DebugReadout.text;
            if (this.meState == eState.eOutOfStorage)
            {
                text = PersistentSettings.GetString("Out_Of_Storage");
            }
            else if (this.meState == eState.eOutOfPower)
            {
                text = PersistentSettings.GetString("Out_Of_Power");
            }
            else if (this.meState == eState.eSearchingForOre)
            {
                text = PersistentSettings.GetString("OE_Searching");
            }
            else if (this.meState == eState.eDrillStuck)
            {
                text = ((this.mnUpdates % 240 != 0) ? PersistentSettings.GetString("Upgrade_Drill_Head") : PersistentSettings.GetString("Drill_Stuck"));
            }
            else if (this.meState == eState.eMining)
            {
                text = PersistentSettings.GetString("Next_Ore") + this.mrTimeUntilNextOre.ToString("F0") + "s";
            }
            else if (this.meState == eState.eVeinDepleted)
            {
                int num = 0;
                if (this.visitedLocations != null)
                {
                    num = this.visitedLocations.Count;
                }
                if (num > 0)
                {
                    text = PersistentSettings.GetString("Clearing_Vein") + num;
                }
            }
            else if (this.meState == eState.eIdle)
            {
                if (this.mnUpdates % 240 < 120)
                {
                    text = PersistentSettings.GetString("OE_Searching");
                    this.DebugReadout.color = Color.white;
                }
                else
                {
                    text = PersistentSettings.GetString("Cant_Find_Ore");
                    this.DebugReadout.color = Color.red;
                }
            }
            if (!text.Equals(this.DebugReadout.text))
            {
                this.DebugReadout.text = text;
            }
        }
    }

    private void UpdateWorkLight()
    {
        if (base.mDotWithPlayerForwards < -10f)
        {
            this.WorkLight.enabled = false;
        }
        else if (base.mVectorToPlayer.y > 16f || base.mVectorToPlayer.y < -16f)
        {
            this.WorkLight.enabled = false;
        }
        else if (base.mDistanceToPlayer > 64f)
        {
            this.WorkLight.enabled = false;
        }
        else
        {
            if (this.meState == eState.eIdle)
            {
                this.WorkLight.color = new Color(1f, 0.75f, 0.1f, 1f);
                float intensity = Mathf.Sin((float)this.mnUpdates / 60f) + 1f;
                this.WorkLight.intensity = intensity;
                this.WorkLight.range = 2f;
            }
            if (this.meState == eState.eFetchingEntryPoint || this.meState == eState.eFetchingExtractionPoint)
            {
                this.WorkLight.color = Color.blue;
                this.WorkLight.enabled = true;
                this.WorkLight.range = 5f;
            }
            if (this.meState == eState.eSearchingForOre)
            {
                this.WorkLight.color = Color.yellow;
            }
            if (this.meState == eState.eMining || this.meState == eState.eVeinDepleted)
            {
                if (base.mDotWithPlayerForwards < 0f)
                {
                    this.MiningSparks.SetActive(false);
                    this.DrillChunks.SetActive(false);
                }
                else
                {
                    if (base.mDistanceToPlayer < 32f)
                    {
                        this.MiningSparks.SetActive(true);
                    }
                    else
                    {
                        this.MiningSparks.SetActive(false);
                    }
                    if (base.mDistanceToPlayer < 8f)
                    {
                        this.DrillChunks.SetActive(true);
                    }
                    else
                    {
                        this.DrillChunks.SetActive(false);
                    }
                }
                this.WorkLight.color = new Color(1f, 0.55f, 0.1f, 1f);
            }
            else
            {
                this.DrillChunks.SetActive(false);
                this.MiningSparks.SetActive(false);
            }
            if (this.meState == eState.eOutOfPower || this.meState == eState.eOutOfPowerVeinDepleted || this.meState == eState.eDrillStuck)
            {
                this.WorkLight.range = 2f;
                this.WorkLight.enabled = true;
                this.WorkLight.color = Color.red;
                this.WorkLight.intensity = Mathf.Sin(this.mrTimeElapsed * 8f) * 4f + 4f;
            }
            if (this.meState == eState.eOutOfStorage || this.meState == eState.eOutOfStorageVeinDepleted)
            {
                this.WorkLight.color = Color.green;
            }
            if (this.meState == eState.eSearchingForOre || this.meState == eState.eOutOfStorage)
            {
                this.WorkLight.intensity = Mathf.Sin(this.mrTimeElapsed * 8f) * 4f + 4f;
                this.WorkLight.enabled = true;
                this.WorkLight.range = 2f;
            }
            else if (this.meState == eState.eMining)
            {
                if (base.mDotWithPlayerForwards > 0f)
                {
                    if (base.mDistanceToPlayer < 32f)
                    {
                        this.mrTimeUntilFlash -= Time.deltaTime;
                    }
                }
                else if (base.mDistanceToPlayer < 4f)
                {
                    this.mrTimeUntilFlash -= Time.deltaTime;
                }
                if (this.mrTimeUntilFlash < 0f)
                {
                    this.mrTimeUntilFlash = 1f;
                    this.WorkLight.intensity = 4f;
                    this.WorkLight.enabled = true;
                    this.WorkLight.range = 5f;
                    if (this.meState == eState.eOutOfPower)
                    {
                        this.WorkLight.intensity = 4f;
                    }
                }
                this.WorkLight.intensity *= 0.75f;
                if (this.WorkLight.intensity < 0.1f)
                {
                    this.WorkLight.enabled = false;
                }
            }
        }
    }

    private void CalcEfficiencyAndDepleteRate()
    {
        this.mnDrillRate = (int)Mathf.Ceil((float)this.GetDrillRateForTier(this.mnDrillTier) / DifficultySettings.mrResourcesFactor);
        this.mnDepleteCount = Mathf.CeilToInt((float)this.mnDrillRate / this.mrEfficiency);
        this.mnBonusOre = 0;
        this.mnBonusDeplete = 0;
        if (this.base_efficiency > 1f)
        {
            this.mnBonusOre = (int)((float)this.mnDrillRate * (this.base_efficiency - 1f));
            this.mnBonusDeplete = (int)((float)this.mnDepleteCount * (this.base_efficiency - 1f));
            Debug.LogWarning("Recalc of Deplete gives " + this.mnBonusOre + " bonus ore and " + this.mnBonusDeplete + " depletion rate.");
        }
        if (this.mnBonusOre < 0)
        {
            Debug.LogError("Error, bonus ore of " + this.mnBonusOre);
        }
        if (this.mnBonusDeplete < 0)
        {
            Debug.LogError("Error, bonus depletion of " + this.mnBonusDeplete);
        }
        this.mnMaxOre = 15 + this.mnDrillRate + this.mnBonusOre;
        if (this.mnDrillRate <= 3)
        {
            this.OreCollectionRate = 1f;
        }
        else if (this.mnDrillRate % 2 != 0)
        {
            if (this.mnDrillRate == 11)
            {
                this.OreCollectionRate = 1f;
            }
            else if (this.mnDrillRate == 21)
            {
                this.OreCollectionRate = 3f;
            }
            else
            {
                Debug.LogError("Error, " + this.mnDrillRate + " doesn't divide into 2!");
                this.OreCollectionRate = 1f;
            }
        }
        else
        {
            this.OreCollectionRate = (float)(this.mnDrillRate / 2);
            if (this.OreCollectionRate > 8f && this.OreCollectionRate % 8f == 0f)
            {
                this.OreCollectionRate /= 8f;
            }
        }
    }

    private int GetDrillRateForTier(int lnDrillTier)
    {
        switch (lnDrillTier)
        {
            case 0:
                return 1;
            case 1:
                return 2;
            case 2:
                return 4;
            case 3:
                return 8;
            case 4:
                return 16;
            case 5:
                return 32;
            default:
                Debug.LogError("Error, drill tier " + lnDrillTier + " is illegal!");
                return -1;
        }
    }

    public void SetTieredUpgrade(int lnTier)
    {
        if (lnTier > 0)
        {
            ARTHERPetSurvival.mbLocatedUpgradedMotor = true;
        }
        this.mnDrillTier = lnTier;
        this.mnDrillRate = this.GetDrillRateForTier(this.mnDrillTier);
        this.mnMaxOre = 15 + this.mnDrillRate + this.mnBonusOre;
        if (DifficultySettings.mrResourcesFactor == 0f)
        {
            Debug.LogError("Extractor loaded before difficulty settings were!");
        }
        this.CalcEfficiencyAndDepleteRate();
        this.mrPowerUsage = 0.5f;
        if (DifficultySettings.mbEasyPower)
        {
            this.mrPowerUsage *= 0.5f;
        }
        if (this.mnDrillTier == 0)
        {
            this.mrPowerUsage *= 0.25f;
        }
        this.mrPowerUsage += (float)(this.mnDrillRate - 1) / 2f;
    }

    public string GetDrillMotorName()
    {
        if (this.mnDrillTier == 0)
        {
            return PersistentSettings.GetString("Economy_Drill_Motor");
        }
        return ItemManager.GetItemName(this.GetDrillMotorID());
    }

    public string GetCutterHeadName()
    {
        if (this.mnCutterTier == 1)
        {
            return PersistentSettings.GetString("Standard_Cutter_Head");
        }
        return ItemManager.GetItemName(this.GetCutterHeadID());
    }

    public void SetCutterUpgrade(ItemDurability cutterHead)
    {
        int num = this.mnCutterTier;
        if (cutterHead == null)
        {
            this.mnCutterTier = 1;
            this.mnCutterDurability = 10000;
        }
        else
        {
            this.mnCutterTier = OreExtractor.GetCutterTierFromItem(cutterHead.mnItemID);
            this.mnCutterDurability = cutterHead.mnCurrentDurability;
        }
        if (this.mnCutterTier > num && base.mDistanceToPlayer < 128f && base.mDotWithPlayerForwards > 0f)
        {
            FloatingCombatTextManager.instance.QueueText(base.mnX, base.mnY + 1, base.mnZ, 1.05f, PersistentSettings.GetString("Upgraded"), Color.cyan, 1f, 64f);
        }
        this.CalculateEfficiency();
        if (this.meState == eState.eDrillStuck && this.CheckHardness())
        {
            this.meState = eState.eSearchingForOre;
        }
        this.CalcEfficiencyAndDepleteRate();
    }

    public static int GetCutterTierFromItem(int itemID)
    {
        if (itemID == OreExtractor.STEEL_HEAD_ID)
        {
            return 2;
        }
        if (itemID == OreExtractor.CRYSTAL_HEAD_ID)
        {
            return 3;
        }
        if (itemID == OreExtractor.ORGANIC_HEAD_ID)
        {
            return 4;
        }
        if (itemID == OreExtractor.PLASMA_HEAD_ID)
        {
            return 5;
        }
        return 1;
    }

    public override bool ShouldSave()
    {
        return true;
    }

    public override void Write(BinaryWriter writer)
    {
        writer.Write(this.mnStoredOre);
        writer.Write(this.mnOreType);
        writer.Write(this.mrCurrentPower);
        writer.Write(this.ExtractionX);
        writer.Write(this.ExtractionY);
        writer.Write(this.ExtractionZ);
        writer.Write(this.mnCutterDurability);
        writer.Write(this.mnCutterTier);
        long value = 0L;
        writer.Write(value);
        writer.Write(this.mnDrillTier);
        float value2 = 0f;
        writer.Write(this.mbReportOffline);
        bool value3 = false;
        writer.Write(value3);
        writer.Write(value3);
        writer.Write(value3);
        writer.Write(this.mrIssueTime);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
        writer.Write(value2);
    }

    public override void Read(BinaryReader reader, int entityVersion)
    {
        this.mnStoredOre = reader.ReadInt32();
        this.mnOreType = reader.ReadUInt16();
        this.mrCurrentPower = reader.ReadSingle();
        this.ExtractionX = reader.ReadInt64();
        this.ExtractionY = reader.ReadInt64();
        this.ExtractionZ = reader.ReadInt64();
        this.mnCutterDurability = reader.ReadInt32();
        this.mnCutterTier = reader.ReadInt32();
        if (this.mnCutterTier == 0)
        {
            this.mnCutterTier = 1;
        }
        if (this.mnCutterTier == 1)
        {
            this.mnCutterDurability = 10000;
        }
        if (!base.mSegment.mbValidateOnly)
        {
            this.CalculateEfficiency();
        }
        reader.ReadInt64();
        this.mnDrillTier = reader.ReadInt32();
        if (this.mnDrillTier <= 0)
        {
            this.mnDrillTier = 0;
        }
        if (this.mnDrillTier > 5)
        {
            this.mnDrillTier = 5;
        }
        if (!base.mSegment.mbValidateOnly)
        {
            this.SetTieredUpgrade(this.mnDrillTier);
        }
        this.mbReportOffline = reader.ReadBoolean();
        bool flag = reader.ReadBoolean();
        flag = reader.ReadBoolean();
        flag = reader.ReadBoolean();
        this.mrIssueTime = reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        reader.ReadSingle();
        if (this.ExtractionX == 0 || (this.ExtractionX == base.mnX && this.ExtractionY == base.mnY && this.ExtractionZ == base.mnZ))
        {
            this.SetNewState(eState.eFetchingEntryPoint);
        }
        else
        {
            this.SetNewState(eState.eFetchingExtractionPoint);
        }
        ARTHERPetSurvival.instance.GotOre(this.mnOreType);
    }

    public override bool ShouldNetworkUpdate()
    {
        return true;
    }

    public override void WriteNetworkUpdate(BinaryWriter writer)
    {
        base.WriteNetworkUpdate(writer);
        writer.Write((int)this.meState);
        writer.Write(this.mrTimeUntilNextOre);
        writer.Write(this.mnEstimatedOreLeft);
        writer.Write(this.mrEfficiency);
        writer.Write(this.mrWorkTime);
        writer.Write(this.mrIdleTime);
        writer.Write(this.EntryX);
        writer.Write(this.EntryY);
        writer.Write(this.EntryZ);
    }

    public override void ReadNetworkUpdate(BinaryReader reader)
    {
        base.ReadNetworkUpdate(reader);
        int num = (int)(this.meState = (eState)reader.ReadInt32());
        this.mrTimeUntilNextOre = reader.ReadSingle();
        this.mnEstimatedOreLeft = reader.ReadInt32();
        this.mrEfficiency = reader.ReadSingle();
        this.mrWorkTime = reader.ReadSingle();
        this.mrIdleTime = reader.ReadSingle();
        this.EntryX = reader.ReadInt64();
        this.EntryY = reader.ReadInt64();
        this.EntryZ = reader.ReadInt64();
    }

    public bool DropStoredOre()
    {
        if (this.mnStoredOre == 0)
        {
            return false;
        }
        ItemManager.DropNewCubeStack(this.mnOreType, TerrainData.GetDefaultValue(this.mnOreType), this.mnStoredOre, base.mnX, base.mnY, base.mnZ, Vector3.zero);
        this.mnStoredOre = 0;
        this.MarkDirtyDelayed();
        return true;
    }

    public ItemBase GetStoredOre()
    {
        if (this.mnOreType == 0)
        {
            return null;
        }
        if (this.mnStoredOre == 0)
        {
            return null;
        }
        return ItemManager.SpawnCubeStack(this.mnOreType, TerrainData.GetDefaultValue(this.mnOreType), this.mnStoredOre);
    }

    public void ClearStoredOre()
    {
        this.mnStoredOre = 0;
        this.MarkDirtyDelayed();
        this.RequestImmediateNetworkUpdate();
    }

    public int GetCutterHeadID()
    {
        if (this.mnCutterTier > 1)
        {
            return 198 + this.mnCutterTier;
        }
        return -1;
    }

    public void DropCurrentCutterHead()
    {
        if (this.mnCutterTier > 1)
        {
            if (this.mnCutterTier > 1)
            {
                Debug.LogWarning("Dropping cutter head!");
                ItemDurability itemDurability = ItemManager.SpawnItem(this.GetCutterHeadID()) as ItemDurability;
                itemDurability.mnCurrentDurability = this.mnCutterDurability;
                ItemManager.instance.DropItem(itemDurability, base.mnX, base.mnY, base.mnZ, Vector3.zero);
            }
            this.mnCutterTier = 1;
            this.mnCutterDurability = 10000;
            this.CalculateEfficiency();
            this.MarkDirtyDelayed();
        }
    }

    public ItemBase GetCutterHead()
    {
        if (this.mnCutterTier > 1)
        {
            ItemDurability itemDurability = ItemManager.SpawnItem(this.GetCutterHeadID()) as ItemDurability;
            itemDurability.mnCurrentDurability = this.mnCutterDurability;
            return itemDurability;
        }
        return null;
    }

    public bool IsValidCutterHead(ItemBase newCutterHeadItem)
    {
        return newCutterHeadItem.mnItemID >= OreExtractor.STEEL_HEAD_ID && newCutterHeadItem.mnItemID <= OreExtractor.PLASMA_HEAD_ID;
    }

    public void SwapCutterHead(ItemBase newCutterHeadItem)
    {
        if (newCutterHeadItem == null)
        {
            this.SetCutterUpgrade(null);
            this.MarkDirtyDelayed();
        }
        else
        {
            if (!this.IsValidCutterHead(newCutterHeadItem))
            {
                throw new AssertException("Tried to set extractor cutter head to invalid cutter head item: " + newCutterHeadItem.mnItemID);
            }
            this.SetCutterUpgrade(newCutterHeadItem as ItemDurability);
            this.MarkDirtyDelayed();
        }
    }

    public bool AttemptUpgradeCutterHead(Player player)
    {
        Debug.LogWarning("AttemptUpgradeCutterHead!");
        if (player != WorldScript.mLocalPlayer)
        {
            return false;
        }
        ItemBase itemBase = null;
        int num = this.mnCutterTier;
        foreach (ItemBase item in player.mInventory)
        {
            if (item.mType == ItemType.ItemDurability)
            {
                int cutterTierFromItem = OreExtractor.GetCutterTierFromItem(item.mnItemID);
                if (cutterTierFromItem > num)
                {
                    num = cutterTierFromItem;
                    itemBase = item;
                }
            }
        }
        if (itemBase != null)
        {
            Debug.LogWarning("Located new, higher tier of cutter head");
            player.mInventory.RemoveSpecificItem(itemBase);
            this.DropCurrentCutterHead();
            this.SetCutterUpgrade(itemBase as ItemDurability);
            this.MarkDirtyDelayed();
            return true;
        }
        Debug.LogWarning("Player had nothing to upgrade the head to");
        return false;
    }

    public int GetDrillMotorID()
    {
        if (this.mnDrillTier > 0)
        {
            return 2999 + this.mnDrillTier;
        }
        return -1;
    }

    public void DropCurrentDrillMotor()
    {
        if (this.mnDrillTier > 0)
        {
            ItemBase item = ItemManager.SpawnItem(this.GetDrillMotorID());
            if (ItemManager.instance.DropItem(item, base.mnX, base.mnY, base.mnZ, Vector3.zero) == null)
            {
                Debug.LogError("ERROR! ORE EXTRACTOR FAILED TO DROPEXISTING DRILL MOTOR!");
            }
        }
        this.mnDrillTier = 0;
        this.MarkDirtyDelayed();
    }

    public bool IsValidMotor(ItemBase newMotorItem)
    {
        if (newMotorItem.mnItemID >= 3000 && newMotorItem.mnItemID <= 3004)
        {
            return this.mnDrillTier != newMotorItem.mnItemID - 2999;
        }
        return false;
    }

    public ItemBase GetMotor()
    {
        if (this.mnDrillTier > 0)
        {
            return ItemManager.SpawnItem(this.GetDrillMotorID());
        }
        return null;
    }

    public void SwapMotor(ItemBase newMotorItem)
    {
        if (newMotorItem == null)
        {
            this.SetTieredUpgrade(0);
            this.MarkDirtyDelayed();
        }
        else if (this.mnDrillTier != newMotorItem.mnItemID - 2999)
        {
            if (!this.IsValidMotor(newMotorItem))
            {
                throw new AssertException("Tried to set extractor drill motor to invalid motor item: " + newMotorItem.mnItemID);
            }
            int tieredUpgrade = newMotorItem.mnItemID - 2999;
            this.SetTieredUpgrade(tieredUpgrade);
            this.MarkDirtyDelayed();
        }
    }

    public bool AttemptUpgradeDrillMotor(Player player)
    {
        if (player != WorldScript.mLocalPlayer)
        {
            return false;
        }
        int num = 0;
        int num2 = 5;
        while (num2 > 0)
        {
            if (player.mInventory.GetItemCount(2999 + num2) <= 0)
            {
                num2--;
                continue;
            }
            num = num2;
            break;
        }
        if (this.mnDrillTier < num)
        {
            player.mInventory.RemoveItem(2999 + num, 1);
            this.DropCurrentDrillMotor();
            this.SetTieredUpgrade(num);
            this.MarkDirtyDelayed();
            return true;
        }
        return false;
    }

    public override void OnDelete()
    {
        if (WorldScript.mbIsServer)
        {
            this.DropCurrentDrillMotor();
            this.DropCurrentCutterHead();
            this.DropStoredOre();
        }
        base.OnDelete();
    }

    public bool PlayerExtractStoredOre(Player player)
    {
        if (player == WorldScript.mLocalPlayer && !player.mInventory.CollectValue(this.mnOreType, 0, this.mnStoredOre))
        {
            return false;
        }
        this.mnStoredOre = 0;
        this.MarkDirtyDelayed();
        this.RequestImmediateNetworkUpdate();
        return true;
    }

    public float GetRemainingPowerCapacity()
    {
        return this.mrMaxPower - this.mrCurrentPower;
    }

    public float GetMaximumDeliveryRate()
    {
        return 10f + (float)(this.mnDrillRate * 3);
    }

    public float GetMaxPower()
    {
        return this.mrMaxPower;
    }

    public bool DeliverPower(float amount)
    {
        if (amount > this.GetRemainingPowerCapacity())
        {
            return false;
        }
        this.mrCurrentPower += amount;
        this.MarkDirtyDelayed();
        return true;
    }

    public bool WantsPowerFromEntity(SegmentEntity entity)
    {
        return true;
    }

    public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
    {
        HolobaseEntityCreationParameters holobaseEntityCreationParameters = new HolobaseEntityCreationParameters(this);
        holobaseEntityCreationParameters.RequiresUpdates = true;
        holobaseEntityCreationParameters.AddVisualisation(holobase.OreExtractor);
        return holobase.CreateHolobaseEntity(holobaseEntityCreationParameters);
    }

    public override void HolobaseUpdate(Holobase holobase, HoloMachineEntity holoMachineEntity)
    {
        if (this.meState == eState.eMining || this.meState == eState.eVeinDepleted)
        {
            holoMachineEntity.VisualisationObjects[0].transform.Search("Drill Rot").gameObject.GetComponent<RotateConstantlyScript>().ZRot = 8f;
            holobase.SetColour(holoMachineEntity.VisualisationObjects[0], Color.yellow);
        }
        else
        {
            GameObject gameObject = holoMachineEntity.VisualisationObjects[0].transform.Search("Drill Rot").gameObject;
            gameObject.GetComponent<RotateConstantlyScript>().ZRot *= 0.9f;
            float num = 1f;
            if (holobase.mnUpdates % 60 < 30)
            {
                num = 0f;
            }
            Color lCol = new Color(num, 0f, 0f, 1f);
            if (this.meState == eState.eDrillStuck)
            {
                lCol = new Color(num, 1f - num, 0f, 1f);
            }
            if (this.meState == eState.eOutOfPower)
            {
                lCol = new Color(num, 0f, 1f - num, 1f);
            }
            if (this.meState == eState.eOutOfStorage)
            {
                lCol = new Color(num, 1f - num, 1f - num, 1f);
            }
            holobase.SetColour(holoMachineEntity.VisualisationObjects[0], lCol);
            gameObject = gameObject.transform.Search("Drill GFX").gameObject;
            holobase.SetTint(gameObject, new Color(1f - num, 0f, 0f, 1f));
        }
    }

    public void ProcessStorageSupplier(StorageMachineInterface storage)
    {
        if (this.mnStoredOre > 0)
        {
            this.mnStoredOre -= storage.TryPartialInsert(this, this.mnOreType, TerrainData.GetDefaultValue(this.mnOreType), this.mnStoredOre);
        }
    }

    public override string GetPopupText()
    {

        string @string = this.MachineName;
        string text = @string;
        @string = text + ".\n" + PersistentSettings.GetString("Power") + " " + this.mrCurrentPower.ToString("F0") + "/" + this.mrMaxPower.ToString("F0");
        string text2 = TerrainData.GetNameForValue(this.mnOreType, 0);
        if (!WorldScript.mLocalPlayer.mResearch.IsKnown(this.mnOreType, 0))
        {
            text2 = "Unknown Material";
        }
        text = @string;
        @string = text + "\n(T) : " + PersistentSettings.GetString("Offline_Warning") + " : " + this.mbReportOffline;
        if (Input.GetKeyDown(KeyCode.T))
        {
            InfExtractorMachineWindow.ToggleReport(WorldScript.mLocalPlayer, this);
        }
        string cutterHeadName = this.GetCutterHeadName();
        if (this.meState == eState.eDrillStuck)
        {
            @string = @string + "\n" + PersistentSettings.GetString("Drill_Stuck") + ".";
            @string += cutterHeadName;
            @string = @string + " " + PersistentSettings.GetString("Cant_Dig") + "\n";
            @string = @string + text2 + ". " + PersistentSettings.GetString("Fit_Head");
        }
        else
        {
            if (this.mnOreType == 0)
            {
                @string = @string + "\n" + PersistentSettings.GetString("OE_Searching");
            }
            else
            {
                text = @string;
                @string = text + "\n" + PersistentSettings.GetString("Next") + " " + text2 + " " + PersistentSettings.GetString("In") + this.mrTimeUntilNextOre.ToString("F0");
            }
            text = @string;
            @string = text + ". " + PersistentSettings.GetString("Total_Stored") + this.mnStoredOre;
        }
        float num = (float)this.mnDrillRate / 1f * DifficultySettings.mrResourcesFactor;
        num = (float)this.mnDrillRate;
        @string += string.Format("\n{0}. {1}: {2:P0}", this.GetDrillMotorName(), PersistentSettings.GetString("Drill_Rate"), num);
        float num2 = (float)this.mnCutterDurability / 10000f;
        @string += string.Format("\n{0} : {1:P0} {2}", cutterHeadName, num2, PersistentSettings.GetString("Durability"));
        text = @string;
        @string = text + "\n" + PersistentSettings.GetString("Work_Efficiency") + " : " + this.mrEfficiency.ToString("P0") + " " + PersistentSettings.GetString("Q_Reset");
        int num3 = (int)((float)this.mnDrillRate * 60f / 30f);
        float num4 = this.mrPowerUsage * DifficultySettings.mrResourcesFactor;
        text = @string;
        @string = text + "\n" + num3 + " " + PersistentSettings.GetString("Ore_Per_Min") + ". " + PersistentSettings.GetString("Demand") + num4.ToString("F2") + " " + PersistentSettings.GetString("Power_Per_Second");
        text = @string;
        @string = text + "\n" + PersistentSettings.GetString("Average_PPS") + " : " + this.mrAveragePPS.ToString("F2");
        float num5 = this.mrWorkTime + this.mrIdleTime;
        float num6 = this.mrWorkTime / num5;
        text = @string;
        @string = text + "\n" + PersistentSettings.GetString("Work_Efficiency") + " : " + num6.ToString("P2") + " - " + PersistentSettings.GetString("Q_Reset");
        if (Input.GetKey(KeyCode.Q))
        {
            this.mrWorkTime = 0f;
            this.mrIdleTime = 0f;
        }
        if (Input.GetButton("Extract") && Input.GetKey(KeyCode.LeftControl) && InfExtractorMachineWindow.DropStoredOre(WorldScript.mLocalPlayer, this))
        {
            AudioHUDManager.instance.Pick();
            Achievements.instance.UnlockAchievement(Achievements.eAchievements.eExtractedOre, false);
        }
        return @string;
    }
}
