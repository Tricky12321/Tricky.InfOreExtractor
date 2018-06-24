using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadVandal.FortressCraft
{
    /// <summary>
    /// Utility class for changing the generic machine panel.
    /// </summary>
    public class GenericMachinePanelHelper : MonoBehaviour
    {
        /// <summary>
        /// Label settings class.
        /// </summary>
        private class LabelSettings
        {
            public int X;
            public int Y;
            public NGUIText.Alignment Alignment;
            public int Width;
            public UILabel.Overflow Overflow;


            /// <summary>
            /// Constructor for Label settings.
            /// </summary>
            /// <param name="x">X position.</param>
            /// <param name="y">Y position.</param>
            /// <param name="alignment">Alignment.</param>
            /// <param name="width">Width.</param>
            /// <param name="overflow">Overflow method.</param>
            public LabelSettings(int x, int y, NGUIText.Alignment alignment, int width, UILabel.Overflow overflow)
            {
                X = x;
                Y = y;
                Alignment = alignment;
                Width = width;
                Overflow = overflow;
            }
        }

        /// <summary>
        /// Running non-static instance.
        /// </summary>
        private static GenericMachinePanelHelper mInstance;

        /// <summary>
        /// Reference to top right corner sprite.
        /// </summary>
        private static Transform mTopRightCorner;

        /// <summary>
        /// Reference to bottom left corner sprite.
        /// </summary>
        private static Transform mBottomLeftCorner;

        /// <summary>
        /// Reference to scroll view panel.
        /// </summary>
        private static UIPanel mScrollViewPanel;

        /// <summary>
        /// Machine panel transform.
        /// </summary>
        private static Transform mMachinePanel;

        /// <summary>
        /// UIScrollView component.
        /// </summary>
        private static UIScrollView mScrollView;

        /// <summary>
        /// Scroll bar transform.
        /// </summary>
        private static Transform mScrollBar;

        /// <summary>
        /// Drag box collider.
        /// </summary>
        private static BoxCollider mDragBoxCollider;

        /// <summary>
        /// Scroll bar sprite.
        /// </summary>
        private static UISprite mScrollBarSprite;

        /// <summary>
        /// Title label.
        /// </summary>
        private static UILabel mTitleLabel;

        /// <summary>
        /// Background panel.
        /// </summary>
        private static UISprite mGenericMachineBackgroundSmall;

        /// <summary>
        /// Original scroll bar height.
        /// </summary>
        private static int mOriginalScrollBarHeight;

        /// <summary>
        /// Original background atlas.
        /// </summary>
        private static UIAtlas mOriginalBackgroundAtlas;

        /// <summary>
        /// Original background sprite name.
        /// </summary>
        private static string mOriginalBackgroundSpriteName;

        /// <summary>
        /// Original background color.
        /// </summary>
        private static Color mOriginalBackgroundColor;

        /// <summary>
        /// Original background sprite type.
        /// </summary>
        private static UIBasicSprite.Type mOriginalBackgroundSpriteType;

        /// <summary>
        /// Original clipping region.
        /// </summary>
        private static Vector4 mOriginalClipRegion;

        /// <summary>
        /// Original drag collider center.
        /// </summary>
        private static Vector3 mOriginalDragColliderCenter;

        /// <summary>
        /// Original drag collider size.
        /// </summary>
        private static Vector3 mOriginalDragColliderSize;

        /// <summary>
        /// Original background size.
        /// </summary>
        private static Vector2 mOriginalBackgroundSize;

        /// <summary>
        /// Original title width.
        /// </summary>
        private static int mOriginalTitleWidth;

        /// <summary>
        /// Dictionary of original positions by transfer.
        /// </summary>
        private static readonly Dictionary<Transform, Vector3> mOriginalPositions = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// Initialized state.
        /// </summary>
        private static bool mInitialized;

        /// <summary>
        /// Indicates if a new position was applied.
        /// </summary>
        private static bool mPositioningApplied;

        /// <summary>
        /// Applied width.
        /// </summary>
        private static int mAppliedWidth;

        /// <summary>
        /// Applied height.
        /// </summary>
        private static int mAppliedHeight;

        /// <summary>
        /// Applied X offset.
        /// </summary>
        private static float mAppliedXOffset;

        /// <summary>
        /// Applied Y offset.
        /// </summary>
        private static float mAppliedYOffset;

        /// <summary>
        /// Applied padding.
        /// </summary>
        private static RectOffset mAppliedPadding;

        /// <summary>
        /// Applied background atlas.
        /// </summary>
        private static UIAtlas mAppliedBackgroundAtlas;

        /// <summary>
        /// Applied background sprite name.
        /// </summary>
        private static string mAppliedBackgroundSpriteName;

        /// <summary>
        /// Indicates scroll should be reset on the next update.
        /// </summary>
        private static bool mResetScrollOnNextUpdate;

        /// <summary>
        /// Dictionary of label settings by name.
        /// </summary>
        private static readonly Dictionary<string, LabelSettings> mLabelSettingsByName = new Dictionary<string, LabelSettings>();

        /// <summary>
        /// Dictionary of icon background positions by name.
        /// </summary>
        private static readonly Dictionary<string, Vector3> mIconBackgroundPositionsByName = new Dictionary<string, Vector3>();


        /// <summary>
        /// Handle Update.
        /// </summary>
        private void Update()
        {
            if (!mPositioningApplied)
                return;

            if (mResetScrollOnNextUpdate)
            {

                // Resets the limits to the bounds.
                mScrollView.UpdateScrollbars(true);

                // Removes pent up mouse wheel scroll which ResetPosition won't.
                mScrollView.Scroll(0);

                // Resets scroll to top.
                mScrollView.ResetPosition();

                // Correct for strange problem on first time machine window launch.
                mScrollView.transform.localPosition = new Vector3(0, -55, 0);

                mResetScrollOnNextUpdate = false;
            }

            UpdatePanelPositions();
            UpdateContent();
        }



        /// <summary>
        /// Sets the background sprite atlas and sprite name to be shown.
        /// </summary>
        /// <param name="atlas">Atlas instance.</param>
        /// <param name="spriteName">Sprite nam3 in the atlas.</param>
        public static void SetBackgroundSprite(UIAtlas atlas, string spriteName)
        {
            mAppliedBackgroundAtlas = atlas;
            mAppliedBackgroundSpriteName = spriteName;
        }


        /// <summary>
        /// Sets the size and Y offset of the generic machine panel with the default padding of 34 on each side.
        /// </summary>
        /// <param name="width">Width (Game default is 365).</param>
        /// <param name="height">Height (Game default is 568).</param>
        public static void SetPanelSizeAndPosition(int width, int height)
        {
            SetPanelSizeAndPosition(width, height, new RectOffset(20, 20, 50, 10), 0, 232);
        }


        /// <summary>
        /// Sets the size and Y offset of the generic machine panel with the default padding of 34 on each side.
        /// </summary>
        /// <param name="width">Width (Game default is 365).</param>
        /// <param name="height">Height (Game default is 568).</param>
        /// <param name="xOffset">X offset from center (Game default is 0 for normal and 484 when inventory open).</param>
        public static void SetPanelSizeAndPosition(int width, int height, float xOffset)
        {
            SetPanelSizeAndPosition(width, height, new RectOffset(20, 20, 50, 10), xOffset, 232);
        }


        /// <summary>
        /// Sets the size and Y offset of the generic machine panel with the default padding of 34 on each side.
        /// </summary>
        /// <param name="width">Width (Game default is 365).</param>
        /// <param name="height">Height (Game default is 568).</param>
        /// <param name="xOffset">X offset from center (Game default is 0 for normal and 484 when inventory open).</param>
        /// <param name="yOffset">Y offset from center (Game default is 232).</param>
        public static void SetPanelSizeAndPosition(int width, int height, float xOffset, float yOffset)
        {
            SetPanelSizeAndPosition(width, height, new RectOffset(20, 20, 50, 10), xOffset, yOffset);
        }


        /// <summary>
        /// Sets the size and Y offset of the generic machine panel with the default padding of 34 on each side.
        /// </summary>
        /// <param name="width">Width (Game default is 365).</param>
        /// <param name="height">Height (Game default is 568).</param>
        /// <param name="padding">Padding (Game default is 34 on each side).</param>
        /// <param name="xOffset">X offset from center (Game default is 0 for normal and 484 when inventory open).</param>
        /// <param name="yOffset">Y Offset from center (Game default is 232).</param>
        public static void SetPanelSizeAndPosition(int width, int height, RectOffset padding, float xOffset, float yOffset)
        {
            if (!mInitialized && !Initialize())
                return;

            // Store applied values.
            mAppliedWidth = width;
            mAppliedHeight = height;
            mAppliedPadding = padding;
            mAppliedXOffset = xOffset;
            mAppliedYOffset = yOffset;
            mPositioningApplied = true;

            UpdatePanelPositions();

            try
            {
                // Adjust the clip scroll clip region.
                if (mScrollViewPanel != null)
                    mScrollViewPanel.baseClipRegion = new Vector4(width * 0.5f + 16.0f + padding.left * 0.5f - padding.right * 0.5f,
                        -height * 0.5f - padding.top * 0.5f + padding.bottom * 0.5f,
                        width - padding.left - padding.right, height - padding.top - padding.bottom);

                UpdateContent();

                // Remove spring annoyance, kill any pent up momentum, and stop unnecessary scrolling.
                if (mScrollView != null)
                {
                    mScrollView.momentumAmount = 0;
                    mScrollView.dragEffect = UIScrollView.DragEffect.Momentum;
                    mScrollView.disableDragIfFits = true;
                }

                mPositioningApplied = true;
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - SetPanelSizeAndPosition - Error Updating ScrollView:" + e);
            }
        }


        /// <summary>
        /// Updates panel positions.
        /// </summary>
        private static void UpdatePanelPositions()
        {
            try
            {
                float xCenterOffset = mAppliedWidth * 0.5f;

                // Background panel.
                GenericMachinePanelScript.instance.Background_Panel.transform.localPosition = new Vector3(mAppliedXOffset, mAppliedYOffset);

                // Set corner sprites.
                if (mTopRightCorner != null)
                    mTopRightCorner.transform.localPosition = new Vector3(xCenterOffset - 71.5f, -71.5f, 2);
                if (mBottomLeftCorner != null)
                    mBottomLeftCorner.transform.localPosition = new Vector3(-xCenterOffset + 71.5f, -mAppliedHeight + 71.5f, 2);

                // Adjust background/border sprite.
                if (mGenericMachineBackgroundSmall != null)
                {
                    mGenericMachineBackgroundSmall.transform.localPosition = new Vector3(0, 0, -2);
                    mGenericMachineBackgroundSmall.width = mAppliedWidth;
                    mGenericMachineBackgroundSmall.height = mAppliedHeight;
                    mGenericMachineBackgroundSmall.atlas = mAppliedBackgroundAtlas;
                    mGenericMachineBackgroundSmall.spriteName = mAppliedBackgroundSpriteName;
                }

                // Adjust scroll bar thumb.
                if (mScrollBar != null)
                    mScrollBar.transform.localPosition = new Vector3(mAppliedWidth + 26.5f, -58.8f, -82.2f);
                if (mScrollBarSprite != null)
                    mScrollBarSprite.height = mAppliedHeight - 68;

                // Adjust the drag box collider.
                if (mDragBoxCollider != null)
                {
                    mDragBoxCollider.center = new Vector3(mAppliedWidth * 0.5f, -1024, 0);
                    mDragBoxCollider.size = new Vector3(mAppliedWidth, 2048, 1.0f);
                }

                // Adjust main content panel.
                if (mMachinePanel != null)
                    mMachinePanel.transform.localPosition = new Vector3(mAppliedXOffset - xCenterOffset - 22.5f, mAppliedYOffset, 0);

                // Adjust title label and bars.
                if (mTitleLabel != null)
                {
                    mTitleLabel.width = mAppliedWidth-20;
                    mTitleLabel.transform.localPosition = new Vector3(mAppliedWidth * 0.5f + 25, -29.3f, -1.0f);
                }

                float xShift = -16;

                // Adjust content sub-panels.
                GenericMachinePanelScript.instance.Label_Holder.transform.localPosition = new Vector3(xShift + mAppliedPadding.left,
                    mAppliedPadding.top, GenericMachinePanelScript.instance.Label_Holder.transform.localPosition.z);
                GenericMachinePanelScript.instance.Content_Holder.transform.localPosition = new Vector3(xShift - 4 + mAppliedPadding.left,
                    mAppliedPadding.top, GenericMachinePanelScript.instance.Content_Holder.transform.localPosition.z);
                GenericMachinePanelScript.instance.Content_Icon_Holder.transform.localPosition = new Vector3(xShift + mAppliedPadding.left,
                    mAppliedPadding.top, GenericMachinePanelScript.instance.Content_Icon_Holder.transform.localPosition.z);
                GenericMachinePanelScript.instance.Icon_Holder.transform.localPosition = new Vector3(xShift + mAppliedPadding.left,
                    mAppliedPadding.top, GenericMachinePanelScript.instance.Icon_Holder.transform.localPosition.z);
                GenericMachinePanelScript.instance.Source_Holder.transform.localPosition = new Vector3(xShift + mAppliedPadding.left,
                    mAppliedPadding.top, GenericMachinePanelScript.instance.Source_Holder.transform.localPosition.z);
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - UpdatePanelPositions - Error Updating Positions:" + e);
            }

        }


        /// <summary>
        /// Update panel content.
        /// </summary>
        private static void UpdateContent()
        {
            try
            {
                // Update labels.
                UILabel[] labels = GenericMachinePanelScript.instance.Label_Holder.GetComponentsInChildren<UILabel>();
                foreach (UILabel label in labels)
                {
                    GenericMachineEntryScript script = label.GetComponent<GenericMachineEntryScript>();
                    if (script == null)
                        continue;

                    LabelSettings labelSettings;
                    if (mLabelSettingsByName.TryGetValue(label.name, out labelSettings))
                    {
                        if (labelSettings.Width >= 0)
                            label.width = labelSettings.Width;
                        else
                        {
                            switch (script.type)
                            {
                                case GenericMachineManager.GenericMachineEntry.Big_Font_Label:
                                case GenericMachineManager.GenericMachineEntry.Huge_Font_Label:
                                    label.width = Math.Max(0, mAppliedWidth - mAppliedPadding.left - mAppliedPadding.right);
                                    break;
                                case GenericMachineManager.GenericMachineEntry.ThreeLine_FullWidth_Label:
                                case GenericMachineManager.GenericMachineEntry.OneLine_FullWidth_Label:
                                case GenericMachineManager.GenericMachineEntry.TwoLine_FullWidth_Label:
                                    label.width = Math.Max(0, mAppliedWidth - mAppliedPadding.left - mAppliedPadding.right);
                                    break;
                                case GenericMachineManager.GenericMachineEntry.OneLine_HalfWidth_Label:
                                case GenericMachineManager.GenericMachineEntry.ThreeLine_HalfWidth_Label:
                                case GenericMachineManager.GenericMachineEntry.TwoLine_HalfWidth_Label:
                                    label.width = Math.Max(0, Mathf.CeilToInt((mAppliedWidth - mAppliedPadding.left - mAppliedPadding.right) * 0.5f));
                                    break;
                            }

                        }

                        label.alignment = labelSettings.Alignment;
                        label.overflowMethod = labelSettings.Overflow;
                        label.transform.localPosition = new Vector2(labelSettings.X + mAppliedPadding.left + 21, -labelSettings.Y - mAppliedPadding.top - 50);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - SetPanelSizeAndPosition - Error Updating Labels:" + e);
            }

            try
            {
                // Adjust icon holder size up so preview icon fits (DJ should be fixing this).
                UISprite[] contentSprites = GenericMachinePanelScript.instance.Content_Icon_Holder.GetComponentsInChildren<UISprite>();
                mIconBackgroundPositionsByName.Clear();
                foreach (UISprite sprite in contentSprites)
                {
                    GenericMachineEntryScript script = sprite.GetComponent<GenericMachineEntryScript>();
                    if (script == null)
                        continue;

                    Vector3 position;
                    switch (script.type)
                    {
                        case GenericMachineManager.GenericMachineEntry.Generic_Machine_Icon_Background:
                            sprite.width = 70;
                            sprite.height = 70;
                            mIconBackgroundPositionsByName[script.name + "_icon"] = sprite.transform.localPosition;
                            break;
                        case GenericMachineManager.GenericMachineEntry.Generic_Machine_Icon:
                            if (mIconBackgroundPositionsByName.TryGetValue(script.name, out position))
                                sprite.transform.localPosition = new Vector3(position.x + 11, position.y - 9);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - SetPanelSizeAndPosition - Error Updating icon backgrounds:" + e);
            }

            try
            {
                // Update content.
                UISprite[] contentSprites = GenericMachinePanelScript.instance.Content_Holder.GetComponentsInChildren<UISprite>();
                foreach (UISprite sprite in contentSprites)
                {
                    UISpriteData spriteData = sprite.GetAtlasSprite();
                    if (spriteData == null)
                        continue;
                    GenericMachineEntryScript script = sprite.GetComponent<GenericMachineEntryScript>();
                    if (script == null)
                        continue;

                    switch (script.type)
                    {
                        case GenericMachineManager.GenericMachineEntry.OneLine_FullWidth_Background:
                        case GenericMachineManager.GenericMachineEntry.TwoLine_FullWidth_Background:
                        case GenericMachineManager.GenericMachineEntry.ThreeLine_FullWidth_Background:
                            sprite.width = mAppliedWidth - mAppliedPadding.left - mAppliedPadding.right + 25;
                            spriteData.SetBorder(6, 6, 6, 6);
                            sprite.type = UIBasicSprite.Type.Sliced;
                            break;
                        case GenericMachineManager.GenericMachineEntry.OneLine_HalfWidth_Background:
                        case GenericMachineManager.GenericMachineEntry.TwoLine_HalfWidth_Background:
                        case GenericMachineManager.GenericMachineEntry.ThreeLine_HalfWidth_Background:
                            sprite.width = (int) ((mAppliedWidth - mAppliedPadding.left + 25) * 0.5f);
                            spriteData.SetBorder(6, 6, 6, 6);
                            sprite.type = UIBasicSprite.Type.Sliced;
                            break;

                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - SetPanelSizeAndPosition - Error Updating Content:" + e);
            }



        }


        /// <summary>
        /// Restores the generic machine panel window to its original state.
        /// </summary>
        public static void RestoreOriginalWindowState()
        {
            if (!mInitialized)
                return;

            try
            {
                mPositioningApplied = false;

                GenericMachinePanelScript.instance.Background_Panel.transform.localPosition = mOriginalPositions[GenericMachinePanelScript.instance.Background_Panel.transform];
                if (mTopRightCorner != null)
                    mTopRightCorner.transform.localPosition = mOriginalPositions[mTopRightCorner];
                if (mBottomLeftCorner != null)
                    mBottomLeftCorner.transform.localPosition = mOriginalPositions[mBottomLeftCorner];

                if (mGenericMachineBackgroundSmall != null)
                {
                    mGenericMachineBackgroundSmall.width = (int) mOriginalBackgroundSize.x;
                    mGenericMachineBackgroundSmall.height = (int) mOriginalBackgroundSize.y;
                    mGenericMachineBackgroundSmall.transform.localPosition = mOriginalPositions[mGenericMachineBackgroundSmall.transform];
                    mGenericMachineBackgroundSmall.atlas = mOriginalBackgroundAtlas;
                    mGenericMachineBackgroundSmall.spriteName = mOriginalBackgroundSpriteName;
                    mGenericMachineBackgroundSmall.color = mOriginalBackgroundColor;
                    mGenericMachineBackgroundSmall.type = mOriginalBackgroundSpriteType;
                }

                if (mScrollBar != null)
                    mScrollBar.localPosition = mOriginalPositions[mScrollBar];
                if (mScrollBarSprite != null)
                    mScrollBarSprite.height = mOriginalScrollBarHeight;

                if (mDragBoxCollider != null)
                {
                    mDragBoxCollider.center = mOriginalDragColliderCenter;
                    mDragBoxCollider.size = mOriginalDragColliderSize;
                }

                if (mMachinePanel != null)
                    mMachinePanel.transform.localPosition = mOriginalPositions[mMachinePanel];

                if (mTitleLabel != null)
                {
                    mTitleLabel.width = mOriginalTitleWidth;
                    mTitleLabel.transform.localPosition = mOriginalPositions[mTitleLabel.transform];
                }

                if (mScrollView != null)
                    mScrollView.UpdateScrollbars(true);
                if (mScrollViewPanel != null)
                    mScrollViewPanel.baseClipRegion = mOriginalClipRegion;

                GenericMachinePanelScript.instance.Label_Holder.transform.localPosition =
                    mOriginalPositions[GenericMachinePanelScript.instance.Label_Holder.transform];
                GenericMachinePanelScript.instance.Content_Holder.transform.localPosition =
                    mOriginalPositions[GenericMachinePanelScript.instance.Content_Holder.transform];
                GenericMachinePanelScript.instance.Content_Icon_Holder.transform.localPosition =
                    mOriginalPositions[GenericMachinePanelScript.instance.Content_Icon_Holder.transform];
                GenericMachinePanelScript.instance.Icon_Holder.transform.localPosition =
                    mOriginalPositions[GenericMachinePanelScript.instance.Icon_Holder.transform];
                GenericMachinePanelScript.instance.Source_Holder.transform.localPosition =
                    mOriginalPositions[GenericMachinePanelScript.instance.Source_Holder.transform];

                UILabel[] labels = GenericMachinePanelScript.instance.Label_Holder.GetComponentsInChildren<UILabel>();
                foreach (UILabel label in labels)
                {
                    label.width = 309;
                    label.overflowMethod = UILabel.Overflow.ShrinkContent;
                }

                if (mScrollView != null)
                    mScrollView.UpdateScrollbars(true);
                if (mScrollViewPanel != null)
                    mScrollViewPanel.baseClipRegion = mOriginalClipRegion;
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - RestoreOriginalWindowState - Error:" + e);
            }
        }


        /// <summary>
        /// Resets the scroll bar.
        /// </summary>
        public static void ResetScroll()
        {
            if (mScrollView != null)
            {
                mResetScrollOnNextUpdate = true;
            }
        }


        /// <summary>
        /// Adds a label to the window.
        /// </summary>
        /// <param name="manager">GenericMachineManager instance.</param>
        /// <param name="type">Label type.</param>
        /// <param name="name">Label name.</param>
        /// <param name="initialLabel">Initial text.</param>
        /// <param name="color">Color.</param>
        /// <param name="background">Indicates if a background is be shown.</param>
        /// <param name="x">X position of the label.</param>
        /// <param name="y">Y position of the label.</param>
        /// <param name="alignment">Alignment of the label.</param>
        /// <param name="width">Width of the later or -1 for automatic.</param>
        /// <param name="overflow">Overflow method.</param>
        public static void AddLabel(GenericMachineManager manager, GenericMachineManager.LabelType type, string name, string initialLabel, Color color, bool background, int x, int y,
            NGUIText.Alignment alignment, int width, UILabel.Overflow overflow)
        {
            LabelSettings labelSettings = new LabelSettings(x, y, alignment, width, overflow);
            mLabelSettingsByName[name] = labelSettings;
            manager.AddLabel(type, name, initialLabel, color, background, x, y);
        }


        /// <summary>
        /// Adds a label to the window.
        /// </summary>
        /// <param name="manager">GenericMachineManager instance.</param>
        /// <param name="name">Label name.</param>
        /// <param name="initialLabel">Initial text.</param>
        /// <param name="color">Color.</param>
        /// <param name="x">X position of the label.</param>
        /// <param name="y">Y position of the label.</param>
        /// <param name="alignment">Alignment of the label.</param>
        /// <param name="width">Width of the later or -1 for -1 for auto.</param>
        /// <param name="overflow">Overflow method.</param>
        public static void AddBigLabel(GenericMachineManager manager, string name, string initialLabel, Color color, int x, int y, NGUIText.Alignment alignment, int width,
            UILabel.Overflow overflow)
        {
            LabelSettings labelSettings = new LabelSettings(x, y, alignment, width, overflow);
            mLabelSettingsByName[name] = labelSettings;
            manager.AddBigLabel(name, initialLabel, color, x, y);
        }


        /// <summary>
        /// Adds a label to the window.
        /// </summary>
        /// <param name="manager">GenericMachineManager instance.</param>
        /// <param name="name">Label name.</param>
        /// <param name="initialLabel">Initial text.</param>
        /// <param name="color">Color.</param>
        /// <param name="x">X position of the label.</param>
        /// <param name="y">Y position of the label.</param>
        /// <param name="alignment">Alignment of the label.</param>
        /// <param name="width">Width of the later or -1 for automatic.</param>
        /// <param name="overflow">Overflow method.</param>
        public static void AddHugeLabel(GenericMachineManager manager, string name, string initialLabel, Color color, int x, int y, NGUIText.Alignment alignment, int width,
            UILabel.Overflow overflow)
        {
            LabelSettings labelSettings = new LabelSettings(x, y, alignment, width, overflow);
            mLabelSettingsByName[name] = labelSettings;
            manager.AddHugeLabel(name, initialLabel, color, x, y);
        }


        /// <summary>
        /// Updates a label to the window.
        /// </summary>
        /// <param name="manager">GenericMachineManager instance.</param>
        /// <param name="name">Label name.</param>
        /// <param name="text">New label text.</param>
        /// <param name="color">Color.</param>
        /// <param name="x">X position of the label.</param>
        /// <param name="y">Y position of the label.</param>
        /// <param name="alignment">Alignment of the label.</param>
        /// <param name="width">Width of the later or -1 for auto.</param>
        /// <param name="overflow">Overflow method.</param>
        public static void UpdateLabel(GenericMachineManager manager, string name, string text, Color color, int x, int y, NGUIText.Alignment alignment, int width, UILabel.Overflow overflow)
        {
            LabelSettings labelSettings;
            if (!mLabelSettingsByName.TryGetValue(name, out labelSettings))
                return;

            labelSettings.X = x;
            labelSettings.Y = y;
            labelSettings.Alignment = alignment;
            labelSettings.Width = width;
            labelSettings.Overflow = overflow;
            manager.UpdateLabel(name, text, color);
        }


        /// <summary>
        /// clears all current label settings.
        /// </summary>
        public static void ClearLabelSettings()
        {
            mLabelSettingsByName.Clear();
        }



        /// <summary>
        /// Initializes variables to Unity objects.
        /// </summary>
        /// <returns>True if successful.</returns>
        private static bool Initialize()
        {
            try
            {
                if (GenericMachinePanelScript.instance == null)
                {
                    Debug.Log("GenericMachinePanelHelper - Error: Missing GenericMachinePanelScript instance");
                    return false;
                }

                if (mInstance == null)
                    mInstance = GenericMachinePanelScript.instance.gameObject.AddComponent<GenericMachinePanelHelper>();


                mOriginalPositions[GenericMachinePanelScript.instance.Background_Panel.transform] = GenericMachinePanelScript.instance.Background_Panel.transform.localPosition;

                mMachinePanel = GenericMachinePanelScript.instance.transform;
                if (mMachinePanel == null)
                {
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Generic_Machine_Panel' object");
                    return false;
                }

                mOriginalPositions[mMachinePanel] = mMachinePanel.transform.localPosition;

                mTopRightCorner = GenericMachinePanelScript.instance.Background_Panel.transform.Find("Generic_Machine_CornerTop");
                if (mTopRightCorner == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Generic_Machine_CornerTop' object");
                else
                    mOriginalPositions[mTopRightCorner] = mTopRightCorner.transform.localPosition;

                mBottomLeftCorner = GenericMachinePanelScript.instance.Background_Panel.transform.Find("Generic_Machine_CornerBottom");
                if (mBottomLeftCorner == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Generic_Machine_CornerBottom' object");
                else
                    mOriginalPositions[mBottomLeftCorner] = mBottomLeftCorner.transform.localPosition;

                Transform backgroundSmallTransform = GenericMachinePanelScript.instance.Background_Panel.transform.Find("Generic_Machine_BackgroundSmall");
                if (backgroundSmallTransform != null)
                    mGenericMachineBackgroundSmall = backgroundSmallTransform.GetComponentInChildren<UISprite>();
                if (mGenericMachineBackgroundSmall == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Generic_Machine_BackgroundSmall' object");
                else
                {
                    mOriginalPositions[mGenericMachineBackgroundSmall.transform] = mGenericMachineBackgroundSmall.transform.localPosition;
                    mOriginalBackgroundSize = new Vector2(mGenericMachineBackgroundSmall.width, mGenericMachineBackgroundSmall.height);
                    mAppliedBackgroundAtlas = mOriginalBackgroundAtlas = mGenericMachineBackgroundSmall.atlas;
                    mAppliedBackgroundSpriteName = mOriginalBackgroundSpriteName = mGenericMachineBackgroundSmall.spriteName;
                    mOriginalBackgroundColor = mGenericMachineBackgroundSmall.color;
                    mOriginalBackgroundSpriteType = mGenericMachineBackgroundSmall.type;
                }

                Transform scrollViewGameObject = GenericMachinePanelScript.instance.transform.Find("Scroll_View");
                if (scrollViewGameObject == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Scroll_View' object");
                else
                {
                    mScrollViewPanel = scrollViewGameObject.GetComponent<UIPanel>();
                    if (mScrollViewPanel == null)
                        Debug.Log("GenericMachinePanelHelper - Error: Missing 'Scroll_View' UIPanel object");
                    else
                        mOriginalClipRegion = mScrollViewPanel.baseClipRegion;

                    mScrollView = scrollViewGameObject.GetComponent<UIScrollView>();
                    if (mScrollView == null)
                        Debug.Log("GenericMachinePanelHelper - Error: Missing 'UIScrollView' object");

                    Transform dragColliderTransform = scrollViewGameObject.Find("DragCollider");
                    if (dragColliderTransform != null)
                        mDragBoxCollider = dragColliderTransform.GetComponent<BoxCollider>();
                    if (mDragBoxCollider == null)
                        Debug.Log("GenericMachinePanelHelper - Error: Missing 'DragCollider' object");
                    else
                    {
                        mOriginalDragColliderCenter = mDragBoxCollider.center;
                        mOriginalDragColliderSize = mDragBoxCollider.size;
                    }
                }

                mScrollBar = GenericMachinePanelScript.instance.transform.Find("Scroll_Bar");
                if (mScrollBar == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Scroll_Bar' object");
                else
                {
                    mOriginalPositions[mScrollBar] = mScrollBar.localPosition;
                    mScrollBarSprite = mScrollBar.Find("Handle").GetComponent<UISprite>();
                    if (mScrollBarSprite == null)
                        Debug.Log("GenericMachinePanelHelper - Error: Missing 'Handle' object");
                    else
                        mOriginalScrollBarHeight = mScrollBarSprite.height;
                }

                Transform titleLabelTransform = GenericMachinePanelScript.instance.transform.Find("Generic_Machine_Title_Label");
                if (titleLabelTransform != null)
                    mTitleLabel = titleLabelTransform.GetComponent<UILabel>();
                if (mTitleLabel == null)
                    Debug.Log("GenericMachinePanelHelper - Error: Missing 'Generic_Machine_Title_Label' object");
                else
                {
                    mOriginalTitleWidth = mTitleLabel.width;
                    mOriginalPositions[mTitleLabel.transform] = mTitleLabel.transform.localPosition;
                }

                mOriginalPositions[GenericMachinePanelScript.instance.Label_Holder.transform] =
                    GenericMachinePanelScript.instance.Label_Holder.transform.localPosition;
                mOriginalPositions[GenericMachinePanelScript.instance.Content_Holder.transform] =
                    GenericMachinePanelScript.instance.Content_Holder.transform.localPosition;
                mOriginalPositions[GenericMachinePanelScript.instance.Content_Icon_Holder.transform] =
                    GenericMachinePanelScript.instance.Content_Icon_Holder.transform.localPosition;
                mOriginalPositions[GenericMachinePanelScript.instance.Icon_Holder.transform] =
                    GenericMachinePanelScript.instance.Icon_Holder.transform.localPosition;
                mOriginalPositions[GenericMachinePanelScript.instance.Source_Holder.transform] =
                    GenericMachinePanelScript.instance.Source_Holder.transform.localPosition;

                mInitialized = true;
            }
            catch (Exception e)
            {
                Debug.Log("GenericMachinePanelHelper - Error Initializing:" + e);
            }

            return mInitialized;
        }


    }
}