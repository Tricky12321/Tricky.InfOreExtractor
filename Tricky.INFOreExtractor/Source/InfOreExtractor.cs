using System;
using System.Collections.Generic;
using System.IO;
using MadVandal.FortressCraft;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Tricky.InfiniteOreExtractor
{
    /// <summary>
    /// Infinite Ore Extractor.
    /// </summary>
    public class InfOreExtractor : MachineEntity, PowerConsumerInterface, StorageSupplierInterface, OreExtractorInterface
    {
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

        public int mnDrillRate = 1;
        private int mnVisualCutterTier = -1;
        public int mnDrillTier;
        public int mnCutterTier = 1;
        public int mnCutterDurability;
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

        private Color mCubeColor;
        private string mMachineName;
        private int mTierUpgrade;

        public InfOreExtractor(Segment segment, long x, long y, long z, ushort cube, byte flags, ushort lValue, bool wasLoaded) : base(eSegmentEntity.Mod, SpawnableObjectEnum.OreExtractor, x, y,
            z, cube, flags, lValue, Vector3.zero, segment)
        {
            try
            {
                mbNeedsLowFrequencyUpdate = true;
                mbNeedsUnityUpdate = true;
                mrTimeUntilNextOre = mrExtractionTime;
                searchLocation = CubeCoord.Invalid;
                mnOreType = 0;
                string cubeKey = TerrainData.GetCubeKey(cube, lValue);
                if (cubeKey == "Tricky.StockInfOreExtractor")
                {
                    mTierUpgrade = 0;
                    mMachineName = "Stock Infinite Ore Extractor";
                    mCubeColor = new Color(2f, 0.5f, 0.5f);
                }

                if (cubeKey == "Tricky.AdvancedInfOreExtractor")
                {
                    mTierUpgrade = 1;
                    mMachineName = "Advanced Infinite Ore Extractor";
                    mCubeColor = new Color(0.5f, 0.5f, 4.5f);
                }

                SetNewState(!wasLoaded ? eState.eFetchingEntryPoint : eState.eFetchingExtractionPoint);

                mnCutterTier = 1;
                if (!segment.mbValidateOnly)
                {
                    CalculateEfficiency();
                    SetTieredUpgrade(0);
                }
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }


        public override void CleanUp()
        {
            base.CleanUp();
            queuedLocations = null;
            visitedLocations = null;
        }


        private void UpdateImportantCPH()
        {
            if (DifficultySettings.mbImportantCPH && CentralPowerHub.Destroyed)
            {
                mrCurrentPower *= 0.95f;
                mrNormalisedPower = mrCurrentPower / mrMaxPower;
            }

            if (!Achievements.CheatsOn || Achievements.CHEAT_BaseBrownout <= 0.0)
                return;
            mrCurrentPower *= 0.95f;
        }


        public override void LowFrequencyUpdate()
        {
            try
            {
                UpdateImportantCPH();
                UpdatePlayerDistanceInfo();
                GameManager.mnNumOreExtractors_Transitory++;
                mrNormalisedPower = mrCurrentPower / mrMaxPower;
                mrSparePowerCapacity = mrMaxPower - mrCurrentPower;
                mrReadoutTick -= LowFrequencyThread.mrPreviousUpdateTimeStep;
                if (mrReadoutTick < 0f)
                    mrReadoutTick = 5f;
                UpdateState();
                mrAveragePPS += (float) (((mrCurrentPower - mrCurrentPower) / LowFrequencyThread.mrPreviousUpdateTimeStep - (double) mrAveragePPS) / 8.0);
                if (mnCutterTier == 1)
                    LookForCutterHead();
                LookForSign();
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }

        private bool AttemptAutoUpgrade(int lnID, StorageMachineInterface storageHopper)
        {
            ItemBase itemBase;
            if (storageHopper.TryExtractItems(this, lnID, 1, out itemBase))
            {
                ItemDurability itemDurability = itemBase as ItemDurability;
                if (itemDurability != null)
                {
                    Achievements.UnlockAchievementDelayed(Achievements.eAchievements.eOpinionsareLike);
                    SetCutterUpgrade(itemDurability);
                    return true;
                }
            }

            return false;
        }

        private void LookForSign()
        {
            if (!(FloatingCombatTextManager.instance == null))
            {
                mnSign++;
                long num = mnX;
                long num2 = mnY;
                long num3 = mnZ;
                if (mnSign % 6 == 0)
                {
                    num--;
                }

                if (mnSign % 6 == 1)
                {
                    num++;
                }

                if (mnSign % 6 == 2)
                {
                    num2--;
                }

                if (mnSign % 6 == 3)
                {
                    num2++;
                }

                if (mnSign % 6 == 4)
                {
                    num3--;
                }

                if (mnSign % 6 == 5)
                {
                    num3++;
                }

                Segment segment = AttemptGetSegment(num, num2, num3);
                if (segment != null && segment.IsSegmentInAGoodState())
                {
                    Sign sign = segment.SearchEntity(num, num2, num3) as Sign;
                    if (sign != null)
                    {
                        if (!(mName == string.Empty) && !(mName != sign.mText))
                        {
                            return;
                        }

                        mName = sign.mText;
                        FloatingCombatTextManager.instance.QueueText(mnX, mnY, mnZ, 0.75f, mName, Color.green, 1f, 16f);
                    }
                }
            }
        }

        private void LookForCutterHead()
        {
            if (WorldScript.mbIsServer)
            {
                lnCutter++;
                long num = mnX;
                long num2 = mnY;
                long num3 = mnZ;
                if (lnCutter % 6 == 0)
                {
                    num--;
                }

                if (lnCutter % 6 == 1)
                {
                    num++;
                }

                if (lnCutter % 6 == 2)
                {
                    num2--;
                }

                if (lnCutter % 6 == 3)
                {
                    num2++;
                }

                if (lnCutter % 6 == 4)
                {
                    num3--;
                }

                if (lnCutter % 6 == 5)
                {
                    num3++;
                }

                Segment segment = AttemptGetSegment(num, num2, num3);
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

                        if (AttemptAutoUpgrade(OreExtractor.PLASMA_HEAD_ID, storageMachineInterface) ||
                            (!AttemptAutoUpgrade(OreExtractor.ORGANIC_HEAD_ID, storageMachineInterface) &&
                             (AttemptAutoUpgrade(OreExtractor.CRYSTAL_HEAD_ID, storageMachineInterface) || !AttemptAutoUpgrade(OreExtractor.STEEL_HEAD_ID, storageMachineInterface))))
                        {
                            ;
                        }
                    }
                }
            }
        }

        public void UpdateState()
        {
            if (meState == eState.eOutOfStorage)
            {
                if (mnStoredOre + mnDrillRate + mnBonusOre < mnMaxOre)
                    SetNewState(eState.eMining);
                mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            else if (meState == eState.eOutOfStorageVeinDepleted)
            {
                if (mnStoredOre + mnDrillRate + mnBonusOre < mnMaxOre)
                    SetNewState(eState.eVeinDepleted);
                else
                    mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }

            if (meState != eState.eDrillStuck)
                mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;

            if (meState == eState.eFetchingEntryPoint)
                UpdateFetchEntryPoint();

            if (meState == eState.eFetchingExtractionPoint)
                UpdateFetchExtractionPoint();

            if (meState == eState.eSearchingForOre)
                DoVeinSearch();

            if (meState == eState.eMining)
            {
                UpdateMining();
                mrIssueTime = 0f;
                mrWorkTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
            }
            else mrIdleTime += LowFrequencyThread.mrPreviousUpdateTimeStep;

            // Should handle this because a vanilla extractor could be on the same vein.
            if (meState == eState.eVeinDepleted)
            {
                UpdateVeinDepleted();
                mrIssueTime = 0.0f;
            }

            mrWorkEfficiency = mrWorkTime / (mrWorkTime + mrIdleTime);

            if (meState == eState.eVeinDepleted)
            {
                UpdateVeinDepleted();
                mrIssueTime = 0f;
            }

            if (meState == eState.eOutOfPower)
            {
                mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
                if (mrCurrentPower > mrPowerUsage)
                    SetNewState(eState.eMining);
            }

            if (meState == eState.eOutOfPowerVeinDepleted)
            {
                mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
                if (mrCurrentPower > mrPowerUsage)
                    SetNewState(eState.eVeinDepleted);
            }

            if (meState == eState.eIdle)
            {
                mrIssueTime += LowFrequencyThread.mrPreviousUpdateTimeStep;
                if (mFrustrum != null)
                    DropMachineFrustrum();
            }

            if (mbReportOffline && mrIssueTime > 0.0)
            {
                float num1 = 300f;
                float num2 = mrIssueTime;
                if (meState == eState.eOutOfStorage)
                    num1 = 1800f;
                if (meState == eState.eOutOfPowerVeinDepleted || meState == eState.eOutOfStorageVeinDepleted || (meState == eState.eVeinDepleted || meState == eState.eDrillStuck))
                    num1 = 60f;
                if (num2 < num1)
                    num2 = -1f;
                if (num2 > 0.0 && num2 > ARTHERPetSurvival.OreErrorTime)
                {
                    ARTHERPetSurvival.OreErrorTime = num2;
                    ARTHERPetSurvival.OreErrorType = mnOreType;
                    if (mnOreType == 0)
                        ARTHERPetSurvival.OreErrorType = 1;
                    ARTHERPetSurvival.ExtractorName = mName;
                    if (meState == eState.eOutOfPower)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfPower;
                    if (meState == eState.eOutOfStorage)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfStorage;
                    if (meState == eState.eDrillStuck)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eDrillStuck;
                    if (mnOreType == 0)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfOre;
                    if (meState == eState.eIdle)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfOre;
                    if (meState == eState.eOutOfPowerVeinDepleted)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfOre;
                    if (meState == eState.eOutOfStorageVeinDepleted)
                        ARTHERPetSurvival.ExtractorIssueType = ARTHERPetSurvival.eExtractorIssueType.eOutOfOre;
                }

                if (mrIssueTime <= 300.0)
                    return;
                ++GameManager.mnNumOreExtractors_With_Issues_Transitory;
            }
        }

        public void UpdateFetchEntryPoint()
        {
            EntryX = EntryY = EntryZ = 0L;
            Vector3 directionVector = CubeHelper.GetDirectionVector((byte) (mFlags & 63U));
            if (CheckNeighbourEntryCube(directionVector))
            {
                if (EntryX == 0L)
                    return;
                RotateExtractorTo(directionVector);
            }
            else
            {
                Vector3 forward = SegmentCustomRenderer.GetRotationQuaternion(mFlags) * Vector3.forward;
                forward.Normalize();
                if (CheckNeighbourEntryCube(forward))
                    return;
                if (CheckNeighbourEntryCube(Vector3.forward))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.forward);
                }
                else if (CheckNeighbourEntryCube(Vector3.back))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.back);
                }
                else if (CheckNeighbourEntryCube(Vector3.left))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.left);
                }
                else if (CheckNeighbourEntryCube(Vector3.right))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.right);
                }
                else if (CheckNeighbourEntryCube(Vector3.down))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.down);
                }
                else if (CheckNeighbourEntryCube(Vector3.up))
                {
                    if (EntryX == 0L)
                        return;
                    RotateExtractorTo(Vector3.up);
                }
                else
                {
                    EntryX = EntryY = EntryZ = 0L;
                    ExtractionX = ExtractionY = ExtractionZ = 0L;
                    SetNewState(eState.eIdle);
                }
            }
        }

        private bool CheckNeighbourEntryCube(Vector3 forward)
        {
            long num1 = mnX + (long) forward.x;
            long num2 = mnY + (long) forward.y;
            long num3 = mnZ + (long) forward.z;
            ushort cube = WorldScript.instance.GetCube(num1, num2, num3);
            if (cube == 0)
            {
                if (WorldScript.mbIsServer)
                    SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(mFrustrum, num1, num2, num3);
                return true;
            }

            if (!CubeHelper.IsOre(cube))
                return false;
            EntryX = num1;
            EntryY = num2;
            EntryZ = num3;
            mnOreType = cube;
            ARTHERPetSurvival.instance.GotOre(mnOreType);
            SetNewState(eState.eSearchingForOre);
            return true;
        }

        private void RotateExtractorTo(Vector3 direction)
        {
            byte flags;
            if (direction.y > 0.5)
                flags = 132;
            else if (direction.y < -0.5)
            {
                flags = 4;
            }
            else
            {
                int num = 0;
                if (direction.x < -0.5)
                    num = 1;
                else if (direction.z < -0.5)
                    num = 2;
                if (direction.x > 0.5)
                    num = 3;
                flags = (byte) (1 | num << 6);
            }

            if (flags == mFlags)
                return;
            mFlags = flags;
            mSegment.SetFlagsNoChecking((int) (mnX % 16L), (int) (mnY % 16L), (int) (mnZ % 16L), mFlags);
            mSegment.RequestDelayedSave();
            if (mWrapper == null || !mWrapper.mbHasGameObject)
                return;
            mTargetRotation = SegmentCustomRenderer.GetRotationQuaternion(flags);
            mbRotateModel = true;
        }


        public void UpdateFetchExtractionPoint()
        {
            ushort cube;
            if (mFrustrum != null)
            {
                Segment segment = mFrustrum.GetSegment(ExtractionX, ExtractionY, ExtractionZ);
                if (segment == null)
                {
                    SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(mFrustrum, ExtractionX, ExtractionY, ExtractionZ);
                    return;
                }

                if (!segment.mbInitialGenerationComplete || segment.mbDestroyed)
                    return;
                cube = segment.GetCube(ExtractionX, ExtractionY, ExtractionZ);
            }
            else
            {
                cube = WorldScript.instance.GetCube(ExtractionX, ExtractionY, ExtractionZ);
                if (cube == 0)
                    return;
            }

            if (cube == mnOreType)
            {
                ARTHERPetSurvival.instance.GotOre(mnOreType);
                if (mnStoredOre >= mnMaxOre)
                    SetNewState(eState.eOutOfStorage);
                else
                    SetNewState(eState.eMining);
            }
            else
                SetNewState(eState.eSearchingForOre);
        }


        private void UpdateMining()
        {
            if (ExtractionX == 0)
                SetNewState(eState.eSearchingForOre);
            else if (!CheckHardness())
                SetNewState(eState.eDrillStuck);
            else
            {
                mrPowerUsage = 0.5f;
                if (DifficultySettings.mbEasyPower)
                    mrPowerUsage *= 0.5f;
                if (mnDrillTier == 0)
                    mrPowerUsage *= 0.25f;
                mrPowerUsage += (mnDrillRate - 1) / 2f;
                float num = mrPowerUsage * LowFrequencyThread.mrPreviousUpdateTimeStep * DifficultySettings.mrResourcesFactor;
                if (num <= mrCurrentPower)
                {
                    mrCurrentPower -= num;
                    mrTimeUntilNextOre -= LowFrequencyThread.mrPreviousUpdateTimeStep * OreCollectionRate;
                    if (mrTimeUntilNextOre > 0.0 || !WorldScript.mbIsServer)
                        return;

                    Segment segment = mFrustrum == null ? WorldScript.instance.GetSegment(ExtractionX, ExtractionY, ExtractionZ) : mFrustrum.GetSegment(ExtractionX, ExtractionY, ExtractionZ);
                    if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                    {
                        mrTimeUntilNextOre = mrExtractionTime;
                        SetNewState(eState.eFetchingExtractionPoint);
                    }
                    else if (segment.GetCube(ExtractionX, ExtractionY, ExtractionZ) != mnOreType)
                    {
                        mrTimeUntilNextOre = mrExtractionTime;
                        SetNewState(eState.eFetchingEntryPoint);
                    }
                    else
                    {
                        ushort num2 = segment.GetCubeData(ExtractionX, ExtractionY, ExtractionZ).mValue;
                        int num3 = (int) ((mnDepleteCount + mnBonusDeplete) / OreCollectionRate);
                        if (num3 >= num2)
                        {
                            Debug.LogWarning("Ore Extractor doing final Mining extraction before moving to Clearing on next update, due to wanting to remove " + num3 +
                                             " ore when there was only " + num2 + " ore left!");
                            num3 = num2 - 1;
                        }

                        if (num2 > 1)
                        {
                            ushort num4 = num2;
                            if (num3 >= 2560)
                                Debug.LogError("Error, risking overflow - deplete count is " + num3 + " and max ore is " + (ushort) 2560);
                            if (num2 > 2560)
                                num2 = 2560;
                            ushort lData = (ushort) (num2 - (ushort) num3);
                            mnEstimatedOreLeft = lData;
                            if (lData > num4)
                                Debug.LogError("Error! We just removed " + num3 + " ore, but now we have " + lData + " ore left!");

                            // Simply don't change the cube value.
                            //segment.SetCubeValueNoChecking((int) (ExtractionX % 16L), (int) (this.ExtractionY % 16L), (int) (this.ExtractionZ % 16L), lData);
                            //segment.RequestDelayedSave();
                            //if (TerrainData.GetSideTexture(mnOreType, lData) != TerrainData.GetSideTexture(mnOreType, num4))
                            //  segment.RequestRegenerateGraphics();

                            int num5 = (int) ((mnDrillRate + mnBonusOre) / OreCollectionRate);
                            mnStoredOre += num5;
                            GameManager.AddOre(num5, mnOreType);
                            LowerCutterDurability(num5);
                            RequestImmediateNetworkUpdate();
                            MarkDirtyDelayed();
                            mrTimeUntilNextOre = mrExtractionTime;
                            if (mnStoredOre < mnMaxOre)
                                return;
                            SetNewState(eState.eOutOfStorage);
                        }
                        else if (EntryX != 0L)
                            SetNewState(eState.eSearchingForOre);
                        else
                            SetNewState(eState.eFetchingEntryPoint);


                    }
                }
                else SetNewState(eState.eOutOfPower);
            }
        }


        private void LowerCutterDurability(int lnOreCount)
        {
            if (mnCutterTier <= 1)
                return;

            mnCutterDurability -= lnOreCount;
            if (mnCutterDurability > 0)
                return;
            mnCutterTier = 1;
            mnCutterDurability = 10000;
            CalculateEfficiency();
        }


        private void DoVeinSearch()
        {
            if (EntryX == 0L)
            {
                SetNewState(eState.eFetchingEntryPoint);
            }
            else
            {
                ExtractionX = ExtractionY = ExtractionZ = 0L;
                if (searchLocation == CubeCoord.Invalid)
                {
                    queuedLocations = new Queue<CubeCoord>(100);
                    visitedLocations = new HashSet<CubeCoord>();
                    if (!SearchNeighbourLocation(EntryX, EntryY, EntryZ))
                        return;
                }
                else if (!SearchNeighbours())
                    return;

                while (queuedLocations.Count > 0)
                {
                    searchLocation = queuedLocations.Dequeue();
                    visitedLocations.Add(searchLocation);
                    if (!SearchNeighbours())
                        return;
                }

                if (ExtractionX != 0L)
                {
                    if (mnStoredOre >= mnMaxOre)
                        SetNewState(eState.eOutOfStorage);
                    else
                        SetNewState(eState.eMining);
                    queuedLocations = null;
                    visitedLocations = null;
                    if (mFrustrum != null)
                    {
                        Segment segment = mFrustrum.GetSegment(ExtractionX, ExtractionY, ExtractionZ);
                        for (int index = 0; index < mFrustrum.mSegments.Count; ++index)
                        {
                            Segment mSegment = mFrustrum.mSegments[index];
                            if (mSegment != null && mSegment != mFrustrum.mMainSegment && mSegment != segment)
                                SegmentManagerThread.instance.RequestSegmentDropForMachineFrustrum(mFrustrum, mSegment);
                        }
                    }
                }
                else
                {
                    queuedLocations = null;
                    if (mnStoredOre >= mnMaxOre)
                        SetNewState(eState.eOutOfStorageVeinDepleted);
                    else
                        SetNewState(eState.eVeinDepleted);
                }

                searchLocation = CubeCoord.Invalid;
            }
        }

        private bool SearchNeighbours()
        {
            return SearchNeighbourLocation(searchLocation.x + 1L, searchLocation.y, searchLocation.z) && (ExtractionX != 0L ||
                                                                                                                              SearchNeighbourLocation(searchLocation.x - 1L,
                                                                                                                                  searchLocation.y, searchLocation.z) &&
                                                                                                                              (ExtractionX != 0L ||
                                                                                                                               SearchNeighbourLocation(searchLocation.x,
                                                                                                                                   searchLocation.y + 1L, searchLocation.z) &&
                                                                                                                               (ExtractionX != 0L ||
                                                                                                                                SearchNeighbourLocation(searchLocation.x,
                                                                                                                                    searchLocation.y - 1L, searchLocation.z) &&
                                                                                                                                (ExtractionX != 0L ||
                                                                                                                                 SearchNeighbourLocation(searchLocation.x,
                                                                                                                                     searchLocation.y, searchLocation.z + 1L) &&
                                                                                                                                 (ExtractionX != 0L ||
                                                                                                                                  SearchNeighbourLocation(searchLocation.x,
                                                                                                                                      searchLocation.y, searchLocation.z - 1L))))));
        }



        private bool SearchNeighbourLocation(long x, long y, long z)
        {
            Segment segment;
            if (mFrustrum != null)
            {
                segment = mFrustrum.GetSegment(x, y, z);
                if (segment == null)
                {
                    SegmentManagerThread.instance.RequestSegmentForMachineFrustrum(mFrustrum, x, y, z);
                    return false;
                }

                if (!segment.mbInitialGenerationComplete || segment.mbDestroyed)
                    return false;
            }
            else
            {
                segment = WorldScript.instance.GetSegment(x, y, z);
                if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                    return false;
            }

            if (segment.GetCube(x, y, z) == mnOreType)
            {
                CubeCoord cubeCoord = new CubeCoord(x, y, z);
                if (visitedLocations == null)
                {
                    Debug.LogError("Error! visitedLocations is null!");
                    return false;
                }

                if (queuedLocations == null)
                {
                    Debug.LogError("Error! queuedLocations is null!");
                    return false;
                }

                if (!visitedLocations.Contains(cubeCoord) && !queuedLocations.Contains(cubeCoord))
                {
                    if (segment.GetCubeDataNoChecking((int) (x % 16L), (int) (y % 16L), (int) (z % 16L)).mValue > mnDepleteCount + mnBonusDeplete)
                    {
                        ExtractionX = x;
                        ExtractionY = y;
                        ExtractionZ = z;
                        queuedLocations.Clear();
                    }
                    else if (visitedLocations.Count < 16384)
                        queuedLocations.Enqueue(cubeCoord);
                }
            }

            return true;
        }



        private void UpdateVeinDepleted()
        {
            float num1 = mrPowerUsage * LowFrequencyThread.mrPreviousUpdateTimeStep * DifficultySettings.mrResourcesFactor;
            if (num1 <= (double) mrCurrentPower)
            {
                mrCurrentPower -= num1;
                mrTimeUntilNextOre -= LowFrequencyThread.mrPreviousUpdateTimeStep * OreCollectionRate;
                if (!CheckHardness())
                {
                    queuedLocations = null;
                    visitedLocations = null;
                    SetNewState(eState.eDrillStuck);
                }
                else
                {
                    if (mrTimeUntilNextOre > 0.0)
                        return;
                    if (visitedLocations == null)
                    {
                        queuedLocations = null;
                        visitedLocations = null;
                        SetNewState(eState.eIdle);
                        Debug.Log("Ore Extractor cleared vein, going to idle mode");
                    }
                    else
                    {
                        bool flag = false;
                        long x = 0;
                        long y = 0;
                        long z = 0;
                        Segment segment = null;
                        while (visitedLocations.Count > 0 && !flag)
                        {
                            int num2 = -1;
                            x = 0L;
                            y = 0L;
                            z = 0L;
                            foreach (CubeCoord visitedLocation in visitedLocations)
                            {
                                int num3 = (int) Util.Abs(visitedLocation.x - EntryX) + (int) Util.Abs(visitedLocation.y - EntryY) + (int) Util.Abs(visitedLocation.z - EntryZ);
                                if (num3 > num2)
                                {
                                    num2 = num3;
                                    x = visitedLocation.x;
                                    y = visitedLocation.y;
                                    z = visitedLocation.z;
                                }
                            }

                            if (x == 0L)
                                throw new AssertException("Ore Extractor: vein depleted visitedlocations broken");
                            segment = mFrustrum == null ? WorldScript.instance.GetSegment(x, y, z) : mFrustrum.GetSegment(x, y, z);
                            if (segment == null || !segment.mbInitialGenerationComplete || segment.mbDestroyed)
                            {
                                queuedLocations = null;
                                visitedLocations = null;
                                SetNewState(eState.eFetchingEntryPoint);
                                return;
                            }

                            if (segment.GetCube(x, y, z) != mnOreType)
                                visitedLocations.Remove(new CubeCoord(x, y, z));
                            else
                                flag = true;
                        }

                        if (flag)
                        {
                            int num2 = (int) Mathf.Ceil(segment.GetCubeData(x, y, z).mValue * mrEfficiency);
                            LowerCutterDurability(num2);
                            mnStoredOre += num2;
                            GameManager.AddOre(num2, mnOreType);
                            WorldScript.instance.BuildFromEntity(segment, x, y, z, 1, 0);
                            MarkDirtyDelayed();
                            mrTimeUntilNextOre = 0.0f;
                            if (mnStoredOre >= mnMaxOre)
                            {
                                SetNewState(eState.eOutOfStorageVeinDepleted);
                                return;
                            }

                            if (visitedLocations == null)
                                Debug.LogError("visitedLocations is null!");
                            else
                                visitedLocations.Remove(new CubeCoord(x, y, z));
                            if (mDistanceToPlayer < 128.0 && mDotWithPlayerForwards > 0.0)
                                FloatingCombatTextManager.instance.QueueText(mnX, mnY + 1L, mnZ, 0.75f, PersistentSettings.GetString("OE_clearing"), Color.green, 1f, 64f);
                        }

                        if (visitedLocations.Count != 0)
                            return;
                        queuedLocations = null;
                        visitedLocations = null;
                        SetNewState(eState.eIdle);
                        Debug.Log("Ore Extractor cleared vein, going to idle mode");
                    }
                }
            }
            else
            {
                queuedLocations = null;
                SetNewState(eState.eOutOfPowerVeinDepleted);
            }
        }


        private void CalculateEfficiency()
        {
            base_efficiency = 0.1f;
            switch (mnCutterTier)
            {
                case 2:
                    base_efficiency = 0.2f;
                    break;
                case 3:
                    base_efficiency = 0.3f;
                    break;
                case 4:
                    base_efficiency = 0.5f;
                    break;
                case 5:
                    base_efficiency = 4f;
                    break;
            }

            mrEfficiency = base_efficiency * (100f / TerrainData.GetHardness(mnOreType, 0));
            if (mrEfficiency > 1.0)
                mrEfficiency = 1f;
            CalcEfficiencyAndDepleteRate();
        }

        private bool CheckHardness()
        {
            return TerrainData.GetHardness(mnOreType, 0) <= (mTierUpgrade == 1 ? 450 : 250);
        }

        private void SetNewState(eState leNewState)
        {
            if (leNewState == meState)
                return;
            if (leNewState == eState.eVeinDepleted && meState != eState.eOutOfStorageVeinDepleted && meState != eState.eOutOfPowerVeinDepleted)
            {
                ARTHERPetSurvival.ClearingType = mnOreType;
                ARTHERPetSurvival.ExtractorEnteringClearing = true;
                ARTHERPetSurvival.ExtractorName = mName;
            }

            RequestImmediateNetworkUpdate();
            meState = leNewState;
        }


        public override void DropGameObject()
        {
            base.DropGameObject();
            mbLinkedToGO = false;
            if (TutorialEffect != null)
            {
                Object.Destroy(TutorialEffect);
                TutorialEffect = null;
            }

            if (FullStorageTutorial != null)
            {
                Object.Destroy(FullStorageTutorial);
                FullStorageTutorial = null;
            }

            if (!(LowPowerTutorial != null))
                return;
            Object.Destroy(LowPowerTutorial);
            LowPowerTutorial = null;
        }


        public override void UnitySuspended()
        {
            WorkLight = null;
            MiningSparks = null;
            DrillChunks = null;
            mRotCont = null;
            DebugReadout = null;
            DrillHeadObject = null;
            OreExtractorBarThing = null;
            if (TutorialEffect != null)
            {
                Object.Destroy(TutorialEffect);
                TutorialEffect = null;
            }

            if (FullStorageTutorial != null)
            {
                Object.Destroy(FullStorageTutorial);
                FullStorageTutorial = null;
            }

            if (!(LowPowerTutorial != null))
                return;
            Object.Destroy(LowPowerTutorial);
            LowPowerTutorial = null;
        }

        private void LinkToGO()
        {
            if (mWrapper != null && mWrapper.mbHasGameObject)
            {
                if (mWrapper.mGameObjectList == null)
                    Debug.LogError("Ore Extractor missing game object #0?");
                if (mWrapper.mGameObjectList[0].gameObject == null)
                    Debug.LogError("Ore Extractor missing game object #0 (GO)?");
                WorkLight = mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<Light>();
                if (WorkLight == null)
                    Debug.LogError("Ore extractor has missing light?");
                WorkLight.enabled = false;
                MiningSparks = mWrapper.mGameObjectList[0].gameObject.transform.Search("MiningSparks").gameObject;
                DrillChunks = mWrapper.mGameObjectList[0].gameObject.transform.Search("DrillParticles").gameObject;
                OreExtractorModel = mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor").gameObject;
                if (MiningSparks == null)
                    Debug.LogError("Failed to find MiningSparks!");
                DrillChunks.SetActive(false);
                MiningSparks.SetActive(false);
                mRotCont = mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<RotateConstantlyScript>();
                if (mRotCont == null)
                    Debug.LogError("Ore Extractor has missing rotator?");
                DrillHeadObject = mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor Drills").gameObject;
                if (DrillHeadObject == null)
                    Debug.LogError("Ore Extractor Drills missing!");
                OreExtractorBarThing = mWrapper.mGameObjectList[0].gameObject.transform.Search("Ore Extractor Bar Thing").gameObject.GetComponent<Renderer>();
                if (OreExtractorBarThing == null)
                    Debug.LogError("OreExtractorBarThing missing!");
                mPowerMPB = new MaterialPropertyBlock();
                mbLinkedToGO = true;
                DebugReadout = mWrapper.mGameObjectList[0].gameObject.GetComponentInChildren<TextMesh>();
                DebugReadout.text = "Intialising...";
                mrTimeUntilFlash = Random.Range(0, 100) / 100f;
                mnVisualCutterTier = -1;

                // Customization of visual done here.
                MeshRenderer component = OreExtractorModel.GetComponent<MeshRenderer>();
                component.material.SetColor("_Color", mCubeColor);
            }
        }

        private void UpdateTutorial()
        {
            if (WorldScript.meGameMode != eGameMode.eSurvival)
                return;
            if (SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.NowFuckOff)
            {
                if (meState == eState.eOutOfPower)
                {
                    if (LowPowerTutorial == null && MobSpawnManager.mrPreviousBaseThreat < 1.0)
                    {
                        LowPowerTutorial = Object.Instantiate<GameObject>(SurvivalSpawns.instance.OE_NoPower,
                            mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                        LowPowerTutorial.SetActive(true);
                        LowPowerTutorial.transform.parent = mWrapper.mGameObjectList[0].gameObject.transform;
                    }
                }
                else if (LowPowerTutorial != null)
                {
                    Object.Destroy(LowPowerTutorial);
                    LowPowerTutorial = null;
                }

                if (meState == eState.eOutOfStorage && MobSpawnManager.mrPreviousBaseThreat < 1.0)
                {
                    if (FullStorageTutorial == null)
                    {
                        FullStorageTutorial = Object.Instantiate<GameObject>(SurvivalSpawns.instance.OE_NoStorage,
                            mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                        FullStorageTutorial.SetActive(true);
                        FullStorageTutorial.transform.parent = mWrapper.mGameObjectList[0].gameObject.transform;
                    }
                }
                else if (FullStorageTutorial != null)
                {
                    Object.Destroy(FullStorageTutorial);
                    FullStorageTutorial = null;
                }
            }

            if (SurvivalPlayerScript.meTutorialState == SurvivalPlayerScript.eTutorialState.PutPowerIntoExtractor)
            {
                if (TutorialEffect == null)
                {
                    TutorialEffect = Object.Instantiate<GameObject>(SurvivalSpawns.instance.PowerOE,
                        mWrapper.mGameObjectList[0].gameObject.transform.position + Vector3.up + Vector3.up, Quaternion.identity);
                    TutorialEffect.SetActive(true);
                    TutorialEffect.transform.parent = mWrapper.mGameObjectList[0].gameObject.transform;
                }
            }
            else if (TutorialEffect != null)
            {
                Object.Destroy(TutorialEffect);
                TutorialEffect = null;
            }

            if (SurvivalPlayerScript.meTutorialState != SurvivalPlayerScript.eTutorialState.RemoveCoalFromHopper || mnStoredOre != 0)
                return;
            mrTimeUntilNextOre *= 0.5f;
        }

        public override void UnityUpdate()
        {
            try
            {
                mrTimeElapsed += Time.deltaTime;
                if (!mbLinkedToGO)
                {
                    LinkToGO();
                }
                else
                {
                    UpdateTutorial();
                    if (mnVisualCutterTier != mnCutterTier)
                    {
                        mnVisualCutterTier = mnCutterTier;
                        DrillHeadObject.transform.Search("Drillhead Default").gameObject.SetActive(false);
                        DrillHeadObject.transform.Search("Drillhead Steel").gameObject.SetActive(false);
                        DrillHeadObject.transform.Search("Drillhead Crystal").gameObject.SetActive(false);
                        DrillHeadObject.transform.Search("Drillhead Organic").gameObject.SetActive(false);
                        DrillHeadObject.transform.Search("Drillhead Plasma").gameObject.SetActive(false);
                        if (mnVisualCutterTier == 1)
                            DrillHeadObject.transform.Search("Drillhead Default").gameObject.SetActive(true);
                        if (mnVisualCutterTier == 2)
                            DrillHeadObject.transform.Search("Drillhead Steel").gameObject.SetActive(true);
                        if (mnVisualCutterTier == 3)
                            DrillHeadObject.transform.Search("Drillhead Crystal").gameObject.SetActive(true);
                        if (mnVisualCutterTier == 4)
                            DrillHeadObject.transform.Search("Drillhead Organic").gameObject.SetActive(true);
                        if (mnVisualCutterTier == 5)
                            DrillHeadObject.transform.Search("Drillhead Plasma").gameObject.SetActive(true);
                    }

                    if (mDistanceToPlayer < 16.0)
                    {
                        DrillHeadObject.SetActive(true);
                        mRotCont.ZRot = (float) (mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch * (double) mnDrillRate * 0.125);
                    }
                    else
                    {
                        DrillHeadObject.SetActive(false);
                        mRotCont.ZRot = 0.0f;
                    }

                    if (mbRotateModel)
                    {
                        mWrapper.mGameObjectList[0].transform.rotation = mTargetRotation;
                        mbRotateModel = false;
                    }

                    if (meState == eState.eMining)
                    {
                        if (mDistanceToPlayer < 32.0)
                        {
                            if (mrTargetPitch == 0.0)
                                mrTargetPitch = Random.Range(0.95f, 1.05f);
                            if (mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch < (double) mrTargetPitch)
                                mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch += Time.deltaTime * 0.1f;
                        }
                    }
                    else
                        mWrapper.mGameObjectList[0].gameObject.GetComponent<AudioSource>().pitch *= 0.99f;

                    bool flag = true;
                    if (mDistanceToPlayer > 128.0)
                        flag = false;
                    if (mSegment.mbOutOfView)
                        flag = false;
                    if (mbWellBehindPlayer)
                        flag = false;
                    if (flag != DebugReadout.GetComponent<Renderer>().enabled)
                        DebugReadout.GetComponent<Renderer>().enabled = flag;
                    if (flag != OreExtractorModel.GetComponent<Renderer>().enabled)
                        OreExtractorModel.GetComponent<Renderer>().enabled = flag;
                    if (flag)
                    {
                        UpdateTextMesh();
                        mPowerMPB.SetFloat("_GlowMult", (float) (mrNormalisedPower * (double) mrNormalisedPower * 16.0));
                        OreExtractorBarThing.SetPropertyBlock(mPowerMPB);
                    }

                    ++mnUpdates;
                    UpdateWorkLight();
                }
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }


        private void UpdateTextMesh()
        {
            if (mDistanceToPlayer >= 64.0)
                return;
            string str = DebugReadout.text;
            if (meState == eState.eOutOfStorage)
                str = PersistentSettings.GetString("Out_Of_Storage");
            else if (meState == eState.eOutOfPower)
                str = PersistentSettings.GetString("Out_Of_Power");
            else if (meState == eState.eSearchingForOre)
                str = PersistentSettings.GetString("OE_Searching");
            else if (meState == eState.eDrillStuck)
                str = mnUpdates % 240 != 0 ? PersistentSettings.GetString("Upgrade_Drill_Head") : PersistentSettings.GetString("Drill_Stuck");
            else if (meState == eState.eMining)
                str = PersistentSettings.GetString("Next_Ore") + mrTimeUntilNextOre.ToString("F0") + "s";
            else if (meState == eState.eVeinDepleted)
            {
                int num = 0;
                if (visitedLocations != null)
                    num = visitedLocations.Count;
                if (num > 0)
                    str = PersistentSettings.GetString("Clearing_Vein") + num;
            }
            else if (meState == eState.eIdle)
            {
                if (mnUpdates % 240 < 120)
                {
                    str = PersistentSettings.GetString("OE_Searching");
                    DebugReadout.color = Color.white;
                }
                else
                {
                    str = PersistentSettings.GetString("Cant_Find_Ore");
                    DebugReadout.color = Color.red;
                }
            }

            if (str.Equals(DebugReadout.text))
                return;
            DebugReadout.text = str;
        }


        private void UpdateWorkLight()
        {
            if (mDotWithPlayerForwards < -10.0)
                WorkLight.enabled = false;
            else if (mVectorToPlayer.y > 16.0 || mVectorToPlayer.y < -16.0)
                WorkLight.enabled = false;
            else if (mDistanceToPlayer > 64.0)
            {
                WorkLight.enabled = false;
            }
            else
            {
                if (meState == eState.eIdle)
                {
                    WorkLight.color = new Color(1f, 0.75f, 0.1f, 1f);
                    WorkLight.intensity = Mathf.Sin(mnUpdates / 60f) + 1f;
                    WorkLight.range = 2f;
                }

                if (meState == eState.eFetchingEntryPoint || meState == eState.eFetchingExtractionPoint)
                {
                    WorkLight.color = Color.blue;
                    WorkLight.enabled = true;
                    WorkLight.range = 5f;
                }

                if (meState == eState.eSearchingForOre)
                    WorkLight.color = Color.yellow;
                if (meState == eState.eMining || meState == eState.eVeinDepleted)
                {
                    if (mDotWithPlayerForwards < 0.0)
                    {
                        MiningSparks.SetActive(false);
                        DrillChunks.SetActive(false);
                    }
                    else
                    {
                        if (mDistanceToPlayer < 32.0)
                            MiningSparks.SetActive(true);
                        else
                            MiningSparks.SetActive(false);
                        if (mDistanceToPlayer < 8.0)
                            DrillChunks.SetActive(true);
                        else
                            DrillChunks.SetActive(false);
                    }

                    WorkLight.color = new Color(1f, 0.55f, 0.1f, 1f);
                }
                else
                {
                    DrillChunks.SetActive(false);
                    MiningSparks.SetActive(false);
                }

                if (meState == eState.eOutOfPower || meState == eState.eOutOfPowerVeinDepleted || meState == eState.eDrillStuck)
                {
                    WorkLight.range = 2f;
                    WorkLight.enabled = true;
                    WorkLight.color = Color.red;
                    WorkLight.intensity = (float) (Mathf.Sin(mrTimeElapsed * 8f) * 4.0 + 4.0);
                }

                if (meState == eState.eOutOfStorage || meState == eState.eOutOfStorageVeinDepleted)
                    WorkLight.color = Color.green;
                if (meState == eState.eSearchingForOre || meState == eState.eOutOfStorage)
                {
                    WorkLight.intensity = (float) (Mathf.Sin(mrTimeElapsed * 8f) * 4.0 + 4.0);
                    WorkLight.enabled = true;
                    WorkLight.range = 2f;
                }
                else
                {
                    if (meState != eState.eMining)
                        return;
                    if (mDotWithPlayerForwards > 0.0)
                    {
                        if (mDistanceToPlayer < 32.0)
                            mrTimeUntilFlash -= Time.deltaTime;
                    }
                    else if (mDistanceToPlayer < 4.0)
                        mrTimeUntilFlash -= Time.deltaTime;

                    if (mrTimeUntilFlash < 0.0)
                    {
                        mrTimeUntilFlash = 1f;
                        WorkLight.intensity = 4f;
                        WorkLight.enabled = true;
                        WorkLight.range = 5f;
                        if (meState == eState.eOutOfPower)
                            WorkLight.intensity = 4f;
                    }

                    WorkLight.intensity *= 0.75f;
                    if (WorkLight.intensity >= 0.100000001490116)
                        return;
                    WorkLight.enabled = false;
                }
            }
        }

        private void CalcEfficiencyAndDepleteRate()
        {
            mnDrillRate = (int) Mathf.Ceil(GetDrillRateForTier(mnDrillTier) / DifficultySettings.mrResourcesFactor);
            mnDepleteCount = Mathf.CeilToInt(mnDrillRate / mrEfficiency);
            mnBonusOre = 0;
            mnBonusDeplete = 0;
            if (base_efficiency > 1.0)
            {
                mnBonusOre = (int) (mnDrillRate * (base_efficiency - 1.0));
                mnBonusDeplete = (int) (mnDepleteCount * (base_efficiency - 1.0));
            }

            if (mnBonusOre < 0)
                Debug.LogError("Error, bonus ore of " + mnBonusOre);
            if (mnBonusDeplete < 0)
                Debug.LogError("Error, bonus depletion of " + mnBonusDeplete);
            mnMaxOre = 15 + mnDrillRate + mnBonusOre;
            if (mnMaxOre > 100)
                mnMaxOre = 100;
            if (mnDrillRate <= 3)
                OreCollectionRate = 1f;
            else if (mnDrillRate % 2 != 0)
            {
                if (mnDrillRate == 11)
                    OreCollectionRate = 1f;
                else if (mnDrillRate == 21)
                {
                    OreCollectionRate = 3f;
                }
                else
                {
                    Debug.LogError("Error, " + mnDrillRate + " doesn't divide into 2!");
                    OreCollectionRate = 1f;
                }
            }
            else
            {
                OreCollectionRate = mnDrillRate / 2;
                if (OreCollectionRate <= 8.0 || OreCollectionRate % 8.0 != 0.0)
                    return;
                OreCollectionRate /= 8f;
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
                ARTHERPetSurvival.mbLocatedUpgradedMotor = true;
            mnDrillTier = lnTier;
            mnDrillRate = GetDrillRateForTier(mnDrillTier);
            mnMaxOre = 15 + mnDrillRate + mnBonusOre;
            if (DifficultySettings.mrResourcesFactor == 0.0)
                Debug.LogError("Extractor loaded before difficulty settings were!");
            CalcEfficiencyAndDepleteRate();
            mrPowerUsage = 0.5f;
            if (DifficultySettings.mbEasyPower)
                mrPowerUsage *= 0.5f;
            if (mnDrillTier == 0)
                mrPowerUsage *= 0.25f;
            mrPowerUsage += (mnDrillRate - 1) / 2f;
        }

        public string GetDrillMotorName()
        {
            if (mnDrillTier == 0)
                return PersistentSettings.GetString("Economy_Drill_Motor");
            return ItemManager.GetItemName(GetDrillMotorID());
        }

        public string GetCutterHeadName()
        {
            if (mnCutterTier == 1)
                return PersistentSettings.GetString("Standard_Cutter_Head");
            return ItemManager.GetItemName(GetCutterHeadID());
        }


        public void SetCutterUpgrade(ItemDurability cutterHead)
        {
            int mnCutterTier = this.mnCutterTier;
            if (cutterHead == null)
            {
                this.mnCutterTier = 1;
                mnCutterDurability = 10000;
            }
            else
            {
                this.mnCutterTier = OreExtractor.GetCutterTierFromItem(cutterHead.mnItemID);
                mnCutterDurability = cutterHead.mnCurrentDurability;
            }

            if (this.mnCutterTier > mnCutterTier && mDistanceToPlayer < 128.0 && mDotWithPlayerForwards > 0.0)
                FloatingCombatTextManager.instance.QueueText(mnX, mnY + 1L, mnZ, 1.05f, PersistentSettings.GetString("Upgraded"), Color.cyan, 1f, 64f);
            CalculateEfficiency();
            if (meState == eState.eDrillStuck && CheckHardness())
                meState = eState.eSearchingForOre;
            CalcEfficiencyAndDepleteRate();
        }

        public static int GetCutterTierFromItem(int itemID)
        {
            if (itemID == OreExtractor.STEEL_HEAD_ID)
                return 2;
            if (itemID == OreExtractor.CRYSTAL_HEAD_ID)
                return 3;
            if (itemID == OreExtractor.ORGANIC_HEAD_ID)
                return 4;
            return itemID == OreExtractor.PLASMA_HEAD_ID ? 5 : 1;
        }

        public override bool ShouldSave()
        {
            return true;
        }

        public override void Write(BinaryWriter writer)
        {
            try
            {
                writer.Write(mnStoredOre);
                writer.Write(mnOreType);
                writer.Write(mrCurrentPower);
                writer.Write(ExtractionX);
                writer.Write(ExtractionY);
                writer.Write(ExtractionZ);
                writer.Write(mnCutterDurability);
                writer.Write(mnCutterTier);
                long num1 = 0;
                writer.Write(num1);
                writer.Write(mnDrillTier);
                float num2 = 0.0f;
                writer.Write(mbReportOffline);
                bool flag = false;
                writer.Write(flag);
                writer.Write(flag);
                writer.Write(flag);
                writer.Write(mrIssueTime);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
                writer.Write(num2);
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }

        public override void Read(BinaryReader reader, int entityVersion)
        {
            try
            {
                mnStoredOre = reader.ReadInt32();
                mnOreType = reader.ReadUInt16();
                mrCurrentPower = reader.ReadSingle();
                ExtractionX = reader.ReadInt64();
                ExtractionY = reader.ReadInt64();
                ExtractionZ = reader.ReadInt64();
                mnCutterDurability = reader.ReadInt32();
                mnCutterTier = reader.ReadInt32();
                if (mnCutterTier == 0)
                    mnCutterTier = 1;
                if (mnCutterTier == 1)
                    mnCutterDurability = 10000;
                if (!mSegment.mbValidateOnly)
                    CalculateEfficiency();
                reader.ReadInt64();
                mnDrillTier = reader.ReadInt32();
                if (mnDrillTier <= 0)
                    mnDrillTier = 0;
                if (mnDrillTier > 5)
                    mnDrillTier = 5;
                if (!mSegment.mbValidateOnly)
                    SetTieredUpgrade(mnDrillTier);
                mbReportOffline = reader.ReadBoolean();
                bool flag = reader.ReadBoolean();
                flag = reader.ReadBoolean();
                flag = reader.ReadBoolean();
                mrIssueTime = reader.ReadSingle();
                double num1 = reader.ReadSingle();
                double num2 = reader.ReadSingle();
                double num3 = reader.ReadSingle();
                double num4 = reader.ReadSingle();
                double num5 = reader.ReadSingle();
                double num6 = reader.ReadSingle();
                double num7 = reader.ReadSingle();
                double num8 = reader.ReadSingle();
                double num9 = reader.ReadSingle();
                double num10 = reader.ReadSingle();
                double num11 = reader.ReadSingle();
                double num12 = reader.ReadSingle();
                double num13 = reader.ReadSingle();
                if (ExtractionX == 0L || ExtractionX == mnX && ExtractionY == mnY && ExtractionZ == mnZ)
                    SetNewState(eState.eFetchingEntryPoint);
                else
                    SetNewState(eState.eFetchingExtractionPoint);
                ARTHERPetSurvival.instance.GotOre(mnOreType);
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }


        public override bool ShouldNetworkUpdate()
        {
            return true;
        }

        public override void WriteNetworkUpdate(BinaryWriter writer)
        {
            try { 
            base.WriteNetworkUpdate(writer);
            writer.Write((int) meState);
            writer.Write(mrTimeUntilNextOre);
            writer.Write(mnEstimatedOreLeft);
            writer.Write(mrEfficiency);
            writer.Write(mrWorkTime);
            writer.Write(mrIdleTime);
            writer.Write(EntryX);
            writer.Write(EntryY);
            writer.Write(EntryZ);
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }

        public override void ReadNetworkUpdate(BinaryReader reader)
        {
            try { 
            base.ReadNetworkUpdate(reader);
            meState = (eState) reader.ReadInt32();
            mrTimeUntilNextOre = reader.ReadSingle();
            mnEstimatedOreLeft = reader.ReadInt32();
            mrEfficiency = reader.ReadSingle();
            mrWorkTime = reader.ReadSingle();
            mrIdleTime = reader.ReadSingle();
            EntryX = reader.ReadInt64();
            EntryY = reader.ReadInt64();
            EntryZ = reader.ReadInt64();
            }
            catch (Exception e)
            {
                Logging.LogException(e);
            }
        }

        public bool DropStoredOre()
        {
            if (mnStoredOre == 0)
                return false;
            ItemManager.DropNewCubeStack(mnOreType, TerrainData.GetDefaultValue(mnOreType), mnStoredOre, mnX, mnY, mnZ, Vector3.zero);
            mnStoredOre = 0;
            MarkDirtyDelayed();
            return true;
        }

        public ItemBase GetStoredOre()
        {
            if (mnOreType == 0)
                return null;
            if (mnStoredOre == 0)
                return null;
            return ItemManager.SpawnCubeStack(mnOreType, TerrainData.GetDefaultValue(mnOreType), mnStoredOre);
        }

        public void ClearStoredOre()
        {
            mnStoredOre = 0;
            MarkDirtyDelayed();
            RequestImmediateNetworkUpdate();
        }

        public int GetCutterHeadID()
        {
            if (mnCutterTier > 1)
                return 198 + mnCutterTier;
            return -1;
        }

        public void DropCurrentCutterHead()
        {
            if (mnCutterTier <= 1)
                return;
            if (mnCutterTier > 1)
            {
                Debug.LogWarning("Dropping cutter head!");
                ItemDurability itemDurability = ItemManager.SpawnItem(GetCutterHeadID()) as ItemDurability;
                itemDurability.mnCurrentDurability = mnCutterDurability;
                ItemManager.instance.DropItem(itemDurability, mnX, mnY, mnZ, Vector3.zero);
            }

            mnCutterTier = 1;
            mnCutterDurability = 10000;
            CalculateEfficiency();
            MarkDirtyDelayed();
        }

        public ItemBase GetCutterHead()
        {
            if (mnCutterTier <= 1)
                return null;
            ItemDurability itemDurability = ItemManager.SpawnItem(GetCutterHeadID()) as ItemDurability;
            itemDurability.mnCurrentDurability = mnCutterDurability;
            return itemDurability;
        }

        public bool IsValidCutterHead(ItemBase newCutterHeadItem)
        {
            if (newCutterHeadItem.mnItemID >= OreExtractor.STEEL_HEAD_ID)
                return newCutterHeadItem.mnItemID <= OreExtractor.PLASMA_HEAD_ID;
            return false;
        }

        public void SwapCutterHead(ItemBase newCutterHeadItem)
        {
            if (newCutterHeadItem == null)
            {
                SetCutterUpgrade(null);
                MarkDirtyDelayed();
            }
            else
            {
                if (!IsValidCutterHead(newCutterHeadItem))
                    throw new AssertException("Tried to set extractor cutter head to invalid cutter head item: " + newCutterHeadItem.mnItemID);
                SetCutterUpgrade(newCutterHeadItem as ItemDurability);
                MarkDirtyDelayed();
            }
        }

        public bool AttemptUpgradeCutterHead(Player player)
        {
            Debug.LogWarning("AttemptUpgradeCutterHead!");
            if (player != WorldScript.mLocalPlayer)
                return false;
            ItemBase itemBase1 = null;
            int num = mnCutterTier;
            foreach (ItemBase itemBase2 in player.mInventory)
            {
                if (itemBase2.mType == ItemType.ItemDurability)
                {
                    int cutterTierFromItem = OreExtractor.GetCutterTierFromItem(itemBase2.mnItemID);
                    if (cutterTierFromItem > num)
                    {
                        num = cutterTierFromItem;
                        itemBase1 = itemBase2;
                    }
                }
            }

            if (itemBase1 != null)
            {
                Debug.LogWarning("Located new, higher tier of cutter head");
                player.mInventory.RemoveSpecificItem(itemBase1);
                DropCurrentCutterHead();
                SetCutterUpgrade(itemBase1 as ItemDurability);
                MarkDirtyDelayed();
                return true;
            }

            Debug.LogWarning("Player had nothing to upgrade the head to");
            return false;
        }

        public int GetDrillMotorID()
        {
            if (mnDrillTier > 0)
                return 2999 + mnDrillTier;
            return -1;
        }

        public void DropCurrentDrillMotor()
        {
            if (mnDrillTier > 0)
            {
                ItemBase itemBase = ItemManager.SpawnItem(GetDrillMotorID());
                if (ItemManager.instance.DropItem(itemBase, mnX, mnY, mnZ, Vector3.zero) == null)
                    Debug.LogError("ERROR! ORE EXTRACTOR FAILED TO DROPEXISTING DRILL MOTOR!");
            }

            mnDrillTier = 0;
            MarkDirtyDelayed();
        }


        public bool IsValidMotor(ItemBase newMotorItem)
        {
            if (newMotorItem.mnItemID < 3000 || newMotorItem.mnItemID > 3004)
                return false;
            return mnDrillTier != newMotorItem.mnItemID - 2999;
        }

        public ItemBase GetMotor()
        {
            if (mnDrillTier > 0)
                return ItemManager.SpawnItem(GetDrillMotorID());
            return null;
        }

        public void SwapMotor(ItemBase newMotorItem)
        {
            if (newMotorItem == null)
            {
                SetTieredUpgrade(0);
                MarkDirtyDelayed();
            }
            else
            {
                if (mnDrillTier == newMotorItem.mnItemID - 2999)
                    return;
                if (!IsValidMotor(newMotorItem))
                    throw new AssertException("Tried to set extractor drill motor to invalid motor item: " + newMotorItem.mnItemID);
                SetTieredUpgrade(newMotorItem.mnItemID - 2999);
                MarkDirtyDelayed();
            }
        }

        public bool AttemptUpgradeDrillMotor(Player player)
        {
            if (player != WorldScript.mLocalPlayer)
                return false;
            int lnTier = 0;
            for (int index = 5; index > 0; --index)
            {
                if (player.mInventory.GetItemCount(2999 + index) > 0)
                {
                    lnTier = index;
                    break;
                }
            }

            if (mnDrillTier >= lnTier)
                return false;
            player.mInventory.RemoveItem(2999 + lnTier, 1);
            DropCurrentDrillMotor();
            SetTieredUpgrade(lnTier);
            MarkDirtyDelayed();
            return true;
        }

        public override void OnDelete()
        {
            if (WorldScript.mbIsServer)
            {
                DropCurrentDrillMotor();
                DropCurrentCutterHead();
                DropStoredOre();
            }

            base.OnDelete();
        }

        public bool PlayerExtractStoredOre(Player player)
        {
            if (player == WorldScript.mLocalPlayer && !player.mInventory.CollectValue(mnOreType, 0, mnStoredOre))
                return false;
            ARTHERPetSurvival.instance.GotOre(mnOreType);
            mnStoredOre = 0;
            MarkDirtyDelayed();
            RequestImmediateNetworkUpdate();
            return true;
        }


        public float GetRemainingPowerCapacity()
        {
            return mrMaxPower - mrCurrentPower;
        }

        public float GetMaximumDeliveryRate()
        {
            return 10f + mnDrillRate * 3;
        }

        public float GetMaxPower()
        {
            return mrMaxPower;
        }

        public bool DeliverPower(float amount)
        {
            if (amount > (double) GetRemainingPowerCapacity())
                return false;
            mrCurrentPower += amount;
            MarkDirtyDelayed();
            return true;
        }

        public bool WantsPowerFromEntity(SegmentEntity entity)
        {
            return true;
        }

        public override HoloMachineEntity CreateHolobaseEntity(Holobase holobase)
        {
            HolobaseEntityCreationParameters parameters = new HolobaseEntityCreationParameters(this);
            parameters.RequiresUpdates = true;
            parameters.AddVisualisation(holobase.OreExtractor);
            return holobase.CreateHolobaseEntity(parameters);
        }


        public override void HolobaseUpdate(Holobase holobase, HoloMachineEntity holoMachineEntity)
        {
            if (meState == eState.eMining || meState == eState.eVeinDepleted)
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
                if (meState == eState.eDrillStuck)
                {
                    lCol = new Color(num, 1f - num, 0f, 1f);
                }

                if (meState == eState.eOutOfPower)
                {
                    lCol = new Color(num, 0f, 1f - num, 1f);
                }

                if (meState == eState.eOutOfStorage)
                {
                    lCol = new Color(num, 1f - num, 1f - num, 1f);
                }

                holobase.SetColour(holoMachineEntity.VisualisationObjects[0], lCol);
                gameObject = gameObject.transform.Search("Drill GFX").gameObject;
                holobase.SetTint(gameObject, new Color(1f - num, 0f, 0f, 1f));
            }
        }

        public string Name
        {
            get { return mName; }
        }

        public ushort OreType
        {
            get { return mnOreType; }
        }

        public string DrillMotorName
        {
            get { return GetDrillMotorName(); }
        }

        public float Efficiency
        {
            get { return mrEfficiency; }
        }

        public int DrillTier
        {
            get { return mnDrillTier; }
        }

        public int DrillRate
        {
            get { return mnDrillRate; }
        }

        public float BonusOre
        {
            get { return mnBonusOre; }
        }

        public OreExtractor.eState State
        {
            get { return (OreExtractor.eState) (int) meState; }
        }

        public float IssueTime
        {
            get { return mrIssueTime; }
            set { mrIssueTime = value; }
        }

        public bool ReportOffline
        {
            get { return mbReportOffline; }
            set { mbReportOffline = value; }
        }

        public string CutterHeadName
        {
            get { return GetCutterHeadName(); }
        }

        public int CutterTier
        {
            get { return mnCutterTier; }
        }

        public int CutterHeadID
        {
            get { return GetCutterHeadID(); }
        }

        public ItemBase CutterHead
        {
            get { return GetCutterHead(); }
        }

        public int CutterDurability
        {
            get { return mnCutterDurability; }
        }

        public float CurrentPower
        {
            get { return mrCurrentPower; }
            set { mrCurrentPower = value; }
        }

        public void ProcessStorageSupplier(StorageMachineInterface storage)
        {
            if (mnStoredOre <= 0)
                return;
            int num = storage.TryPartialInsert(this, mnOreType, TerrainData.GetDefaultValue(mnOreType), mnStoredOre);
            mnStoredOre -= num;
            if (num == 0)
                return;
            mrIssueTime = 0.0f;
            ARTHERPetSurvival.instance.GotOre(mnOreType);
        }


        public override string GetPopupText()
        {
            InfOreExtractor extractor = this;
            string str1 = mMachineName + " - " + string.Format(PersistentSettings.GetString("Power_X_X"), extractor.mrCurrentPower.ToString("F0"),
                              extractor.mrMaxPower.ToString("F0"));
            string str2 = TerrainData.GetNameForValue(extractor.mnOreType, 0);
            if (!WorldScript.mLocalPlayer.mResearch.IsKnown(extractor.mnOreType, 0))
                str2 = "Unknown Material";
            string str3 = str1 + "\n(T) : " + PersistentSettings.GetString("Offline_Warning") + " : " + extractor.mbReportOffline;
            if (Input.GetKeyDown(KeyCode.T))
            {
                InfExtractorMachineWindow.ToggleReport(WorldScript.mLocalPlayer, extractor);
                MarkDirtyDelayed();
            }

            string cutterHeadName = extractor.GetCutterHeadName();
            string str4 = extractor.meState != eState.eDrillStuck
                ? (extractor.mnOreType != (ushort) 0
                      ? str3 + "\n" + string.Format(PersistentSettings.GetString("Next_X_in_X"), str2, extractor.mrTimeUntilNextOre.ToString("F0"))
                      : str3 + "\n" + PersistentSettings.GetString("OE_Searching")).ToString() + PersistentSettings.GetString("Total_Stored") + extractor.mnStoredOre
                : str3 + "\n" + PersistentSettings.GetString("Drill_Stuck") + "." + cutterHeadName + " " + PersistentSettings.GetString("Cant_Dig") + "\n" + str2 + ". " +
                  PersistentSettings.GetString("Fit_Head");
            float num1 = extractor.mnDrillRate / 1f * DifficultySettings.mrResourcesFactor;
            float mnDrillRate = extractor.mnDrillRate;
            string str5 = str4 + string.Format("\n{0}. {1}: {2:P0}", extractor.GetDrillMotorName(), PersistentSettings.GetString("Drill_Rate"), mnDrillRate);
            float num2 = extractor.mnCutterDurability / 10000f;
            if (extractor.mnCutterTier == 0)
                str5 += string.Format("\n{0} : {1:P0} {2}", cutterHeadName, num2, PersistentSettings.GetString("Durability"));
            int num3 = (int) (extractor.mnDrillRate * 60.0 / 30.0);
            float num4 = extractor.mrPowerUsage * DifficultySettings.mrResourcesFactor;
            string str6 = str5 + "\n" + PersistentSettings.GetString("Average_PPS") + " : " + extractor.mrAveragePPS.ToString("F2");
            float num5 = extractor.mrWorkTime + extractor.mrIdleTime;
            float num6 = extractor.mrWorkTime / num5;
            string str7 = str6 + "\n" + PersistentSettings.GetString("Work_Efficiency") + " : " + num6.ToString("P2") + " - " + PersistentSettings.GetString("Q_Reset");
            if (Input.GetKey(KeyCode.Q))
            {
                extractor.mrWorkTime = 0.0f;
                extractor.mrIdleTime = 0.0f;
            }

            if (Input.GetButton("Extract") && Input.GetKey(KeyCode.LeftControl) && InfExtractorMachineWindow.DropStoredOre(WorldScript.mLocalPlayer, extractor))
            {
                AudioHUDManager.instance.Pick();
                Achievements.instance.UnlockAchievement(Achievements.eAchievements.eExtractedOre, false);
            }

            return str7;
        }

    }
}
