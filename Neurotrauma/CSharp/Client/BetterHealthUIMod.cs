using System.Reflection;
using System.Collections.Generic;
using System;
using Barotrauma;
using Barotrauma.Sounds;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace BetterHealthUI {
    partial class BetterHealthUIMod {

        private int healthGUIRefreshTimer = 0;
        public static bool NeurotraumaEnabled = false;
        private static Random rnd = new Random();
        private static CharacterHealth prevOpenHealthWindow = null;

        public void InitClient() {

            // Check if Neurotrauma is enabled
            foreach (ContentPackage package in ContentPackageManager.EnabledPackages.All)
            {
                if (package.NameMatches("Neurotrauma"))
                {
                    NeurotraumaEnabled = true;
                    break;
                }
            }
            if (NeurotraumaEnabled)
            {
                ecgCurve = new CCurve();
                ecgCurve.Keys.Add(new CCurveKey(0,0));           // start
                ecgCurve.Keys.Add(new CCurveKey(0.1f,-0.1f));    // first low
                ecgCurve.Keys.Add(new CCurveKey(0.2f,1f));       // high
                ecgCurve.Keys.Add(new CCurveKey(0.3f,-0.3f));    // second low
                ecgCurve.Keys.Add(new CCurveKey(0.4f, 0f));      // end

                ecgCurveFib = new CCurve();
                ecgCurveFib.Keys.Add(new CCurveKey(0, -0.9f));    
                ecgCurveFib.Keys.Add(new CCurveKey(0.05f, -0.2f));
                ecgCurveFib.Keys.Add(new CCurveKey(0.1f, 0.3f));  
                ecgCurveFib.Keys.Add(new CCurveKey(0.15f, -0.2f));
                ecgCurveFib.Keys.Add(new CCurveKey(0.2f, -0.9f)); 
            }

            //LuaCsSetup.PrintCsMessage("BetterHealthUIMod.InitClient");
            foreach (Character character in Character.CharacterList)
            {
                CharacterHealth charHealth = character.CharacterHealth;
                ForceCustomized(charHealth);
            }

            void ForceCustomized(CharacterHealth selfHealth)
            {
                GUIListBox afflictionIconContainer = (GUIListBox)((typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(selfHealth));

                var afflictionIconContainer2 = afflictionIconContainer != null ? (afflictionIconContainer.Parent.GetChildByUserData("afflictionIconContainer2")) : null;
                if (afflictionIconContainer2 == null && afflictionIconContainer!= null)
                {
                    // we havent added our custom stuff yet, do it NOW!
                    LuaCsSetup.PrintCsMessage("Forcing customization on " + selfHealth.Character.Name);

                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add("character", selfHealth.Character);
                    InitProjSpecific(selfHealth, args);
                }
            }

            // health window init
            // changes dimensions of the health window
            // changes job icon, character portrait and name layout and color
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_InitProjSpecific",
                typeof(CharacterHealth).GetMethod("InitProjSpecific", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    InitProjSpecific(self, args);
                    return null;
                }, LuaCsHook.HookMethodType.After, this);
            void InitProjSpecific(object self, Dictionary<string, object> args)
            {
                #region Reflection crap
                // get arguments
                Character character = (Character)(args["character"]);
                // get members
                CharacterHealth selfHealth = (CharacterHealth)self;
                GUITextBlock characterName = (GUITextBlock)(typeof(CharacterHealth).GetField("characterName", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                GUIFrame healthWindow = (GUIFrame)(typeof(CharacterHealth).GetField("healthWindow", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                #endregion

                var healthWindowVerticalLayout = (GUILayoutGroup)(healthWindow.GetChild(0));
                var characterIndicatorArea = (GUILayoutGroup)(healthWindowVerticalLayout.GetChild(3));

                // set size of health window
                healthWindow.RectTransform.RelativeSize = new Vector2(0.8f, 0.6f);

                // clear portrait, name and icon
                var topContainer = characterName.RectTransform.Parent;
                topContainer.ClearChildren();

                // create own layout
                topContainer.Parent.RelativeSize = new Vector2(0.975f, 0.95f);

                // job icon
                var jobIcon = new GUICustomComponent(new RectTransform(new Vector2(0.2f, 1.0f), topContainer),
                    onDraw: (spriteBatch, component) =>
                    {
                        character.Info?.DrawJobIcon(spriteBatch, component.Rect, character != Character.Controlled);
                    });
                //jobIcon.RectTransform.RelativeSize *= 1f;
                //jobIcon.RectTransform.SetPosition(Anchor.TopLeft);

                // char portrait
                var characterPortrait = new GUICustomComponent(new RectTransform(new Vector2(0.2f, 1.0f), topContainer, Anchor.CenterLeft),
                    onDraw: (spriteBatch, component) =>
                    {
                        var size = component.Rect.Size;
                        int iconSize = (int)(Math.Min(size.X, size.Y) * 1.8f);
                        Vector2 area = new Vector2(iconSize, iconSize);

                        Vector2 offset = new Vector2(-size.X * 0.15f, size.Y * 0.15f);
                        Vector2 pos = component.Rect.Center.ToVector2() + offset;

                        character.Info?.DrawIcon(spriteBatch, pos, area);
                    });

                characterPortrait.RectTransform.RelativeSize *= 0.7f;
                //characterPortrait.RectTransform.Anchor = Anchor.TopLeft;
                //characterPortrait.RectTransform.RelativeOffset = new Vector2(0f, 0.3f);

                // char name
                characterName = new GUITextBlock(new RectTransform(new Vector2(0.6f, 1f), topContainer, anchor: Anchor.CenterLeft), "", textAlignment: Alignment.CenterLeft, font: GUIStyle.LargeFont)
                {
                    AutoScaleHorizontal = true,
                    AutoScaleVertical = true
                };
                characterName.TextOffset = new Vector2(-50, 0);

                // color the name according to the job
                if (character.Info?.Job?.Prefab.UIColor != null)
                    characterName.TextColor = character.Info.Job.Prefab.UIColor;

                // resize the gene splicer slot
                selfHealth.InventorySlotContainer.RectTransform.RelativeSize *= 0.5f;

                // resize the cpr button
                var cprButton = (GUIButton)(characterIndicatorArea.GetChild(1));
                cprButton.RectTransform.RelativeSize = new Vector2(0.17f, 0.17f);

                // resize the limb man area
                var limbSelection = (GUICustomComponent)(characterIndicatorArea.GetChild(2));
                limbSelection.RectTransform.RelativeSize = new Vector2(0.3f, 1.0f);

                // resize the first affliction info list (non-buff)
                afflictionIconContainer.RectTransform.RelativeSize = new Vector2(0.4f, 1);
                // create the second affliction info list (buff)
                var afflictionIconContainer2 = new GUIListBox(new RectTransform(new Vector2(0.4f, 1.0f), characterIndicatorArea.RectTransform), style: null)
                {
                    UserData = "afflictionIconContainer2",
                };

                // not sure why i need to do this twice but if i dont then the inventoryslot isnt at the right place
                // the first time the health window is opened
                // nvm, even then it doesnt if in singleplayer
                characterIndicatorArea.Recalculate();

                // Neurotrauma specific elements
                if (NeurotraumaEnabled) 
                {
                    var children = healthWindow.RectTransform.Children;
                    List<RectTransform> newChildren = new List<RectTransform>();
                    
                    var graphArea = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 0.15f), healthWindow.RectTransform, anchor : Anchor.TopRight))
                    {
                        Stretch = true,
                        RelativeSpacing = 0.02f,
                    };

                    // rearrange the health windows children so that the graph gets rendered behind the name
                    newChildren.Add(graphArea.RectTransform);
                    foreach(RectTransform child in healthWindow.RectTransform.Children)
                    {
                        if (child == graphArea.RectTransform) continue;
                        newChildren.Add(child);
                    }

                    ((List<RectTransform>)(healthWindow.RectTransform.Children)).Clear();
                    foreach (RectTransform child in newChildren)
                    {
                        ((List<RectTransform>)(healthWindow.RectTransform.Children)).Add(child);
                    }

                    var graph = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.9f), graphArea.RectTransform), style: "GUIFrameListBox");
                    new GUICustomComponent(new RectTransform(new Vector2(0.9f, 0.98f), graph.RectTransform, Anchor.Center), DrawGraph, null);

                    void DrawGraph(SpriteBatch spriteBatch, GUICustomComponent container)
                    {
                        //if (item.Removed) { return; }
                        float maxLoad = 100;// loadGraph.Max();
                        float xOffset = 0;// graphTimer / updateGraphInterval;
                        Rectangle graphRect = new Rectangle(container.Rect.X, container.Rect.Y, container.Rect.Width, container.Rect.Height - (int)(5 * GUI.yScale));
                        DrawHeartGraph(heartGraph, spriteBatch, graphRect, xOffset);
                    }

                    void DrawHeartGraph(IList<float> graph, SpriteBatch spriteBatch, Rectangle rect, float xOffset)
                    {
                        Color color = Color.Green;
                        const float maxVal = 2;

                        Rectangle prevScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
                        spriteBatch.End();
                        spriteBatch.GraphicsDevice.ScissorRectangle = rect;
                        spriteBatch.Begin(SpriteSortMode.Deferred, rasterizerState: GameMain.ScissorTestEnable);

                        float lineWidth = (float)rect.Width / (float)(graph.Count - 2);
                        float yScale = (float)rect.Height / maxVal;

                        Vector2 prevPoint = new Vector2(rect.Left, rect.Bottom - (graph[1] + (graph[0] - graph[1]) * xOffset) * yScale);

                        float currX = rect.Left + ((xOffset - 1.0f) * lineWidth);

                        for (int i = 1; i < graph.Count - 1; i++)
                        {
                            float age = 1 - ((i - heartGraphProgress + graph.Count) % graph.Count) / (float)graph.Count;

                            currX += lineWidth;

                            Vector2 newPoint = new Vector2(currX, rect.Bottom - graph[i] * yScale);

                            color = age < 0.025 ? Color.White:Color.Lerp(Color.LimeGreen, Color.Black, age);
                            GUI.DrawLine(spriteBatch, prevPoint, newPoint + new Vector2(1.0f, 0), color,0,age < 0.025?4:2);

                            prevPoint = newPoint;
                        }

                        Vector2 lastPoint = new Vector2(rect.Right,
                            rect.Bottom - (graph[graph.Count - 1] + (graph[graph.Count - 2] - graph[graph.Count - 1]) * xOffset) * yScale);


                        GUI.DrawLine(spriteBatch, prevPoint, lastPoint, color);


                        spriteBatch.End();
                        spriteBatch.GraphicsDevice.ScissorRectangle = prevScissorRect;
                        spriteBatch.Begin(SpriteSortMode.Deferred);
                    }
                }

                // reflection apply
                typeof(CharacterHealth).GetField("characterName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self, characterName);
            }

            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_UpdateOxygenProjSpecific",
            typeof(CharacterHealth).GetMethod("UpdateOxygenProjSpecific", BindingFlags.Instance | BindingFlags.NonPublic),
            (object self, Dictionary<string, object> args) => {
                // prevent heart sounds through low oxygen if neurotrauma is enabled
                if(NeurotraumaEnabled)
                {
                    if (CharacterHealth.OpenHealthWindow == null&& flatlineSoundChannel!=null)
                    {
                        flatlineSoundChannel.Dispose();
                        flatlineSoundChannel = null;
                    }
                    return true;
                }
                return null;
            }, LuaCsHook.HookMethodType.Before, this);

            // CreateRecommendedTreatments override
            // sets the max amount of displayed treatments to 10
            const int maxDisplayedSuitableTreatments = 10;
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_CreateRecommendedTreatments",
                typeof(CharacterHealth).GetMethod("CreateRecommendedTreatments", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    // get members
                    CharacterHealth selfHealth = (CharacterHealth)self;
                    GUIListBox recommendedTreatmentContainer = (GUIListBox)(typeof(CharacterHealth).GetField("recommendedTreatmentContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                    int selectedLimbIndex = (int)(typeof(CharacterHealth).GetField("selectedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                    //GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));

                    // override begins
                    ItemPrefab prevHighlightedItem = null;
                    if (GUI.MouseOn?.UserData is ItemPrefab && recommendedTreatmentContainer.Content.IsParentOf(GUI.MouseOn))
                    {
                        prevHighlightedItem = (ItemPrefab)GUI.MouseOn.UserData;
                    }

                    recommendedTreatmentContainer.Content.ClearChildren();

                    float characterSkillLevel = Character.Controlled == null ? 0.0f : Character.Controlled.GetSkillLevel("medical");

                    //key = item identifier
                    //float = suitability
                    Dictionary<Identifier, float> treatmentSuitability = new Dictionary<Identifier, float>();
                    selfHealth.GetSuitableTreatments(treatmentSuitability,
                        ignoreHiddenAfflictions: true,
                        limb: selectedLimbIndex == -1 ? null : selfHealth.Character.AnimController.Limbs.Find(l => l.HealthIndex == selectedLimbIndex),
                        user : Character.Controlled);

                    foreach (Identifier treatment in treatmentSuitability.Keys.ToList())
                    {
                        //prefer suggestions for items the player has
                        if (Character.Controlled.Inventory.FindItemByIdentifier(treatment, recursive: true) != null)
                        {
                            treatmentSuitability[treatment] *= 10.0f;
                        }
                    }

                    if (!treatmentSuitability.Any())
                    {
                        new GUITextBlock(new RectTransform(Vector2.One, recommendedTreatmentContainer.Content.RectTransform), TextManager.Get("none"), textAlignment: Alignment.Center)
                        {
                            CanBeFocused = false
                        };
                        recommendedTreatmentContainer.ScrollBarVisible = false;
                        recommendedTreatmentContainer.AutoHideScrollBar = false;
                    }
                    else
                    {
                        recommendedTreatmentContainer.ScrollBarVisible = true;
                        recommendedTreatmentContainer.AutoHideScrollBar = true;
                    }

                    List<KeyValuePair<Identifier, float>> treatmentSuitabilities = treatmentSuitability.OrderByDescending(t => t.Value).ToList();

                    int count = 0;
                    foreach (KeyValuePair<Identifier, float> treatment in treatmentSuitabilities)
                    {
                        if (treatment.Value < 0) { continue; }
                        count++;
                        if (count > maxDisplayedSuitableTreatments) { break; }
                        if (!(MapEntityPrefab.Find(name: null, identifier: treatment.Key, showErrorMessages: false) is ItemPrefab item)) { continue; }

                        var itemSlot = new GUIFrame(new RectTransform(new Vector2(1.0f / (maxDisplayedSuitableTreatments+1.0f), 1.0f), recommendedTreatmentContainer.Content.RectTransform, Anchor.TopLeft),
                            style: null)
                        {
                            UserData = item
                        };

                        var innerFrame = new GUIButton(new RectTransform(Vector2.One, itemSlot.RectTransform, Anchor.Center, Pivot.Center, scaleBasis: ScaleBasis.Smallest), style: "SubtreeHeader")
                        {
                            UserData = item,
                            DisabledColor = Color.White * 0.1f,
                            OnClicked = (btn, userdata) =>
                            {
                                if (!(userdata is ItemPrefab itemPrefab)) { return false; }
                                var item = Character.Controlled.Inventory.FindItem(it => it.Prefab == itemPrefab, recursive: true);
                                if (item == null) { return false; }
                                Limb targetLimb = selfHealth.Character.AnimController.Limbs.FirstOrDefault(l => l.HealthIndex == selectedLimbIndex);
                                item.ApplyTreatment(Character.Controlled, selfHealth.Character, targetLimb);
                                return true;
                            }
                        };

                        new GUIImage(new RectTransform(Vector2.One, innerFrame.RectTransform, Anchor.Center), style: "TalentBackgroundGlow")
                        {
                            CanBeFocused = false,
                            Color = GUIStyle.Green,
                            HoverColor = Color.White,
                            PressedColor = Color.DarkGray,
                            SelectedColor = Color.Transparent,
                            DisabledColor = Color.Transparent
                        };

                        Sprite itemSprite = item.InventoryIcon ?? item.Sprite;
                        Color itemColor = itemSprite == item.Sprite ? item.SpriteColor : item.InventoryIconColor;
                        var itemIcon = new GUIImage(new RectTransform(new Vector2(0.8f, 0.8f), innerFrame.RectTransform, Anchor.Center),
                            itemSprite, scaleToFit: true)
                        {
                            CanBeFocused = false,
                            Color = itemColor * 0.9f,
                            HoverColor = itemColor,
                            SelectedColor = itemColor,
                            DisabledColor = itemColor * 0.8f
                        };

                        if (item == prevHighlightedItem)
                        {
                            innerFrame.State = GUIComponent.ComponentState.Hover;
                            innerFrame.Children.ForEach(c => c.State = GUIComponent.ComponentState.Hover);
                        }
                    }

                    recommendedTreatmentContainer.RecalculateChildren();

                    // got rid of this because it makes the afflictions spazz around
                    /*afflictionIconContainer.Content.RectTransform.SortChildren((r1, r2) =>
                    {
                        var first = r1.GUIComponent.UserData as Affliction;
                        var second = r2.GUIComponent.UserData as Affliction;
                        int dmgPerSecond = Math.Sign(second.DamagePerSecond - first.DamagePerSecond);
                        return dmgPerSecond != 0 ? dmgPerSecond : Math.Sign(second.Strength - first.Strength);
                    });*/

                    if (count > 0)
                    {
                        var treatmentIconSize = recommendedTreatmentContainer.Content.Children.Sum(c => c.Rect.Width + recommendedTreatmentContainer.Spacing);
                        if (treatmentIconSize < recommendedTreatmentContainer.Content.Rect.Width)
                        {
                            var spacing = new GUIFrame(new RectTransform(new Point((recommendedTreatmentContainer.Content.Rect.Width - treatmentIconSize) / 2, 0), recommendedTreatmentContainer.Content.RectTransform), style: null)
                            {
                                CanBeFocused = false
                            };
                            spacing.RectTransform.SetAsFirstChild();
                        }
                    }

                    return true;
                }, LuaCsHook.HookMethodType.Before, this);

            // CreateAfflictionInfoElements override
            // The method responsible for the little info box that appears when hovering over an affliction in the health interface
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_CreateAfflictionInfoElements",
                typeof(CharacterHealth).GetMethod("CreateAfflictionInfoElements", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    // get arguments
                    GUIComponent parent = (GUIComponent)(args["parent"]);
                    Affliction affliction = (Affliction)(args["affliction"]);

                    // get members
                    CharacterHealth selfHealth = (CharacterHealth)self;
                    //LocalizedString[] strengthTexts = (LocalizedString[])(typeof(CharacterHealth).GetField("strengthTexts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(self));

                    // override begins
                    var labelContainer = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.2f), parent.RectTransform), isHorizontal: true)
                    {
                        Stretch = true,
                        AbsoluteSpacing = 10,
                        UserData = "label",
                        CanBeFocused = false
                    };

                    var afflictionName = new GUITextBlock(new RectTransform(new Vector2(0.65f, 1.0f), labelContainer.RectTransform), affliction.Prefab.Name, textAlignment: Alignment.CenterLeft, font: GUIStyle.LargeFont)
                    {
                        CanBeFocused = false,
                        AutoScaleHorizontal = true
                    };
                    var afflictionStrength = new GUITextBlock(new RectTransform(new Vector2(0.35f, 0.6f), labelContainer.RectTransform), "", textAlignment: Alignment.TopRight, font: GUIStyle.SubHeadingFont)
                    {
                        UserData = "strength",
                        CanBeFocused = false
                    };
                    var vitality = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.4f), labelContainer.RectTransform, Anchor.BottomRight), "", textAlignment: Alignment.BottomRight)
                    {
                        Padding = afflictionStrength.Padding,
                        IgnoreLayoutGroups = true,
                        UserData = "vitality",
                        CanBeFocused = false
                    };

                    var description = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), parent.RectTransform),
                        affliction.Prefab.GetDescription(
                            affliction.Strength,
                            selfHealth.Character == Character.Controlled ? AfflictionPrefab.Description.TargetType.Self : AfflictionPrefab.Description.TargetType.OtherCharacter)
                        , textAlignment: Alignment.TopLeft, wrap: true)
                    {
                        CanBeFocused = false
                    };

                    if (description.Font.MeasureString(description.WrappedText).Y > description.Rect.Height)
                    {
                        description.Font = GUIStyle.SmallFont;
                    }

                    Point nameDims = new Point(afflictionName.Rect.Width, (int)(GUIStyle.LargeFont.Size * 1.5f));

                    afflictionStrength.Text = affliction.GetStrengthText();

                    Vector2 strengthDims = GUIStyle.SubHeadingFont.MeasureString(afflictionStrength.Text);

                    labelContainer.RectTransform.Resize(new Point(labelContainer.Rect.Width, nameDims.Y));
                    afflictionName.RectTransform.Resize(new Point((int)(labelContainer.Rect.Width - strengthDims.X * 0.99f), nameDims.Y));
                    afflictionStrength.RectTransform.Resize(new Point(labelContainer.Rect.Width - afflictionName.Rect.Width, nameDims.Y));

                    afflictionStrength.TextColor = Color.Lerp(GUIStyle.Orange, GUIStyle.Red,
                        affliction.Strength / affliction.Prefab.MaxStrength);

                    description.RectTransform.Resize(new Point(description.Rect.Width, (int)(description.TextSize.Y + 10)));

                    int vitalityDecrease = (int)affliction.GetVitalityDecrease(selfHealth);
                    if (vitalityDecrease == 0)
                    {
                        vitality.Visible = false;
                    }
                    else
                    {
                        vitality.Visible = true;
                        vitality.Text = TextManager.Get("Vitality") + " -" + vitalityDecrease;
                        vitality.TextColor = vitalityDecrease <= 0 ? GUIStyle.Green :
                        Color.Lerp(GUIStyle.Orange, GUIStyle.Red, affliction.Strength / affliction.Prefab.MaxStrength);
                    }

                    vitality.AutoDraw = true;

                    return true;
                }, LuaCsHook.HookMethodType.Before, this);

            // DrawHealthWindow override
            // makes it so more than the most severe affliction is displayed (like in legacy (good))
            // also responsible for the limbs color in the health interface
            Vector2 limbGuyOffset = new Vector2(0, 0);
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_DrawHealthWindow",
                typeof(CharacterHealth).GetMethod("DrawHealthWindow", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    // get arguments
                    SpriteBatch spriteBatch = (SpriteBatch)(args["spriteBatch"]);
                    Rectangle drawArea = (Rectangle)(args["drawArea"]);
                    bool allowHighlight = (bool)(args["allowHighlight"]);

                    DrawHealthWindow(spriteBatch, drawArea, allowHighlight,self);
                    return true;
                }, LuaCsHook.HookMethodType.Before, this);
            void DrawHealthWindow(SpriteBatch spriteBatch, Rectangle drawArea, bool allowHighlight,object self)
            {
                #region Reflection crap
                // get members
                CharacterHealth selfHealth = (CharacterHealth)self;
                ForceCustomized(selfHealth);
                //LocalizedString[] strengthTexts = (LocalizedString[])(typeof(CharacterHealth).GetField("strengthTexts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(self));
                List<CharacterHealth.LimbHealth> limbHealths = (List<CharacterHealth.LimbHealth>)(typeof(CharacterHealth).GetField("limbHealths", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                Dictionary<Affliction, CharacterHealth.LimbHealth> afflictions = (Dictionary<Affliction, CharacterHealth.LimbHealth>)(typeof(CharacterHealth).GetField("afflictions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                SpriteSheet limbIndicatorOverlay = (SpriteSheet)(typeof(CharacterHealth).GetField("limbIndicatorOverlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                float limbIndicatorOverlayAnimState = (float)(typeof(CharacterHealth).GetField("limbIndicatorOverlayAnimState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                int highlightedLimbIndex = (int)(typeof(CharacterHealth).GetField("highlightedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                int selectedLimbIndex = (int)(typeof(CharacterHealth).GetField("selectedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                List<Affliction> afflictionsDisplayedOnLimb = (List<Affliction>)(typeof(CharacterHealth).GetField("afflictionsDisplayedOnLimb", BindingFlags.NonPublic | BindingFlags.Static).GetValue(self));
                GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                GUIListBox afflictionIconContainer2 = (GUIListBox)(afflictionIconContainer.Parent.GetChildByUserData("afflictionIconContainer2"));
                #endregion
                // override begins

                // clear heart graph if the open window changed
                if (prevOpenHealthWindow!= CharacterHealth.OpenHealthWindow)
                {
                    prevOpenHealthWindow = CharacterHealth.OpenHealthWindow;
                    if (NeurotraumaEnabled)
                    {
                        for (int j = 0; j < heartGraph.Length; j++) heartGraph[j] = 0;
                        heartGraphProgress = 0;
                    }
                }

                if (selfHealth.Character.Removed) { return; }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, blendState: BlendState.NonPremultiplied, rasterizerState: GameMain.ScissorTestEnable, effect: GameMain.GameScreen.GradientEffect);

                int i = 0;
                foreach (CharacterHealth.LimbHealth limbHealth in limbHealths)
                {
                    if (limbHealth.IndicatorSprite == null) { continue; }

                    Rectangle limbEffectiveArea = new Rectangle(limbHealth.IndicatorSprite.SourceRect.X + limbHealth.HighlightArea.X,
                                                                limbHealth.IndicatorSprite.SourceRect.Y + limbHealth.HighlightArea.Y,
                                                                limbHealth.HighlightArea.Width,
                                                                limbHealth.HighlightArea.Height);

                    float totalDamage = GetTotalDamage(limbHealth, afflictions, selfHealth);

                    //float negativeEffect = 0.0f, positiveEffect = 0.0f;

                    List<object[]> bottomcolors = new List<object[]>();
                    bottomcolors.Add(new object[] { Color.Gray/*new Color(50 / 255f, 66 / 255f, 168 / 255f)*/, 0.02f });
                    List<object[]> topcolors = new List<object[]>();
                    topcolors.Add(new object[] { Color.Gray/*new Color(50/255f, 66/255f, 168/255f)*/, 0.02f });

                    float topstrength = 0.02f;
                    float bottomstrength = 0.02f;

                    foreach (KeyValuePair<Affliction, CharacterHealth.LimbHealth> kvp in afflictions)
                    {
                        var affliction = kvp.Key;
                        if (affliction.Prefab.LimbSpecific)
                        {
                            if (kvp.Value != limbHealth) continue;
                        }
                        else
                        {
                            LimbType displayedType = LimbHealthToLimbType(limbHealth, limbHealths, selfHealth.Character.AnimController.Limbs);
                            if (NormalizeLimbType(affliction.Prefab.IndicatorLimb) != NormalizeLimbType(displayedType)) continue;
                        }

                        if (!affliction.ShouldShowIcon(selfHealth.Character)) { continue; }

                        float strength = MathHelper.Lerp(0.2f, 1, MathHelper.Clamp(affliction.Strength / affliction.Prefab.MaxStrength * 2, 0, 1));

                        if (affliction.Prefab.IsBuff)
                        {
                            topcolors.Add(new object[] { CharacterHealth.GetAfflictionIconColor(affliction), strength });
                            topstrength += strength;
                        }
                        else
                        {
                            bottomcolors.Add(new object[] { CharacterHealth.GetAfflictionIconColor(affliction), strength });
                            bottomstrength += strength;
                        }
                    }

                    float midPoint = (float)(limbEffectiveArea.Center.Y - limbEffectiveArea.Height / 4) / (float)limbHealth.IndicatorSprite.Texture.Height;
                    float fadeDist = 0.3f * (float)limbEffectiveArea.Height / (float)limbHealth.IndicatorSprite.Texture.Height;

                    Color color1 = topstrength < bottomstrength ? AverageColor(bottomcolors) : Color.Lerp(AverageColor(topcolors), AverageColor(bottomcolors), bottomstrength);
                    Color color2 = Color.Lerp(color1, AverageColor(topcolors), topstrength);

                    GameMain.GameScreen.GradientEffect.Parameters["color1"].SetValue(color1.ToVector4());
                    GameMain.GameScreen.GradientEffect.Parameters["color2"].SetValue(color2.ToVector4());
                    GameMain.GameScreen.GradientEffect.Parameters["midPoint"].SetValue(midPoint);
                    GameMain.GameScreen.GradientEffect.Parameters["fadeDist"].SetValue(fadeDist);

                    float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);

                    limbHealth.IndicatorSprite.Draw(spriteBatch,
                        drawArea.Center.ToVector2()+limbGuyOffset, Color.White,
                        limbHealth.IndicatorSprite.Origin,
                        0, scale);

                    if (GameMain.DebugDraw)
                    {
                        Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea,selfHealth);

                        GUI.DrawRectangle(spriteBatch, highlightArea, Color.Red, false);
                        GUI.DrawRectangle(spriteBatch, drawArea, Color.Red, false);
                    }

                    i++;
                }

                spriteBatch.End();

                spriteBatch.Begin(SpriteSortMode.Deferred, Barotrauma.Lights.CustomBlendStates.Multiplicative);

                if (limbIndicatorOverlay != null)
                {
                    float overlayScale = Math.Min(
                        drawArea.Width / (float)limbIndicatorOverlay.FrameSize.X,
                        drawArea.Height / (float)limbIndicatorOverlay.FrameSize.Y);

                    int frame;
                    int frameCount = 17;
                    if (limbIndicatorOverlayAnimState >= frameCount * 2) limbIndicatorOverlayAnimState = 0.0f;
                    if (limbIndicatorOverlayAnimState < frameCount)
                    {
                        frame = (int)limbIndicatorOverlayAnimState;
                    }
                    else
                    {
                        frame = frameCount - (int)(limbIndicatorOverlayAnimState - (frameCount - 1));
                    }

                    limbIndicatorOverlay.Draw(spriteBatch, frame, drawArea.Center.ToVector2()+ limbGuyOffset, Color.Gray, origin: limbIndicatorOverlay.FrameSize.ToVector2() / 2, rotate: 0.0f,
                        scale: Vector2.One * overlayScale);
                }

                if (allowHighlight)
                {
                    i = 0;
                    foreach (CharacterHealth.LimbHealth limbHealth in limbHealths)
                    {
                        if (limbHealth.HighlightSprite == null) { continue; }

                        float scale = Math.Min(drawArea.Width / (float)limbHealth.HighlightSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.HighlightSprite.SourceRect.Height);

                        int drawCount = 0;
                        if (i == highlightedLimbIndex) { drawCount++; }
                        if (i == selectedLimbIndex) { drawCount++; }
                        for (int j = 0; j < drawCount; j++)
                        {
                            limbHealth.HighlightSprite.Draw(spriteBatch,
                                drawArea.Center.ToVector2()+limbGuyOffset, Color.White,
                                limbHealth.HighlightSprite.Origin,
                                0, scale);
                        }
                        i++;
                    }
                }
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, blendState: BlendState.NonPremultiplied, rasterizerState: GameMain.ScissorTestEnable);

                // drawing the preview icons on the limbs
                i = 0;
                foreach (CharacterHealth.LimbHealth limbHealth in limbHealths)
                {
                    afflictionsDisplayedOnLimb.Clear();
                    int negativecount = 0;
                    int positivecount = 0;
                    int undrawncount = 0;
                    foreach (var affliction in afflictions)
                    {
                        if (ShouldDisplayAfflictionOnLimb(affliction, limbHealth,selfHealth,limbHealths))
                        {
                            if (affliction.Key.Prefab.IsBuff)
                            {
                                if (positivecount >= 4) { undrawncount++; continue; }
                                positivecount++;
                            }
                            else
                            {
                                if (negativecount >= 4) { undrawncount++; continue; }
                                negativecount++;
                            }

                            afflictionsDisplayedOnLimb.Add(affliction.Key);

                        }
                    }

                    if (!afflictionsDisplayedOnLimb.Any()) { i++; continue; }
                    if (limbHealth.IndicatorSprite == null) { continue; }

                    float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);

                    Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea,selfHealth);

                    float iconScale = 0.25f * scale;

                    int drawnPositve = 0;
                    int drawnNegative = 0;
                    foreach (Affliction affliction in afflictionsDisplayedOnLimb)
                    {
                        bool isBuff = affliction.Prefab.IsBuff;
                        Vector2 iconPos = highlightArea.Center.ToVector2();
                        if (negativecount > 0 && positivecount > 0)
                            iconPos += new Vector2(10 * (isBuff ? 1 : -1), 0);

                        float spacing = MathHelper.Clamp(40 / (Math.Max(negativecount, positivecount) / 1.5f), 10, 40);

                        iconPos += new Vector2(0, spacing * ((isBuff ? drawnPositve : drawnNegative) - 0.5f * (Math.Max(negativecount, positivecount) - 1)));

                        DrawLimbAfflictionIcon(spriteBatch, affliction, iconScale, ref iconPos);

                        if (isBuff) drawnPositve++;
                        else drawnNegative++;
                    }

                    // draw the "+x" if theres too many afflictions
                    if (undrawncount > 0)
                    {
                        string additionalAfflictionCount = $"+{undrawncount}";
                        Vector2 displace = GUIStyle.SubHeadingFont.MeasureString(additionalAfflictionCount);

                        Vector2 iconPos = highlightArea.Center.ToVector2();
                        if (negativecount > 0 && positivecount > 0)
                            iconPos += new Vector2(10, 0);

                        GUIStyle.SubHeadingFont.DrawString(spriteBatch, additionalAfflictionCount, iconPos + new Vector2(displace.X * 1.1f, -displace.Y * 0.45f), Color.Black * 0.75f);
                        GUIStyle.SubHeadingFont.DrawString(spriteBatch, additionalAfflictionCount, iconPos + new Vector2(displace.X, -displace.Y * 0.5f), Color.White);
                    }

                    i++;
                }

                if (selectedLimbIndex > -1 && (afflictionIconContainer.Content.CountChildren > 0 || afflictionIconContainer2.Content.CountChildren > 0))
                {
                    CharacterHealth.LimbHealth limbHealth = limbHealths[selectedLimbIndex];
                    if (limbHealth?.IndicatorSprite != null)
                    {
                        var target = afflictionIconContainer.Content.CountChildren > 0 ? afflictionIconContainer : afflictionIconContainer2;
                        Rectangle selectedLimbArea = GetLimbHighlightArea(limbHealth, drawArea,selfHealth);
                        GUI.DrawLine(spriteBatch,
                            new Vector2(target.Rect.X, target.Rect.Y),
                            selectedLimbArea.Center.ToVector2(),
                            Color.LightGray * 0.5f, width: 4);
                    }
                }

                if (NeurotraumaEnabled)
                    UpdateGraph();

                void DrawLimbAfflictionIcon(SpriteBatch spriteBatch, Affliction affliction, float iconScale, ref Vector2 iconPos)
                {
                    if (!affliction.ShouldShowIcon(selfHealth.Character) || affliction.Prefab.Icon == null) { return; }
                    Vector2 iconSize = affliction.Prefab.Icon.size * iconScale;

                    float showIconThreshold = Character.Controlled?.CharacterHealth == selfHealth ? affliction.Prefab.ShowIconThreshold : affliction.Prefab.ShowIconToOthersThreshold;

                    //afflictions that have a strength of less than 10 are faded out slightly
                    float alpha = MathHelper.Lerp(0.3f, 1.0f,
                        (affliction.Strength - showIconThreshold) / Math.Min(affliction.Prefab.MaxStrength - showIconThreshold, 10.0f));

                    affliction.Prefab.Icon.Draw(spriteBatch, iconPos - iconSize / 2.0f, CharacterHealth.GetAfflictionIconColor(affliction) * alpha, 0, iconScale);
                    iconPos += new Vector2(10.0f, 20.0f) * iconScale;
                }
            }

            bool ShouldDisplayAfflictionOnLimb(KeyValuePair<Affliction, CharacterHealth.LimbHealth> kvp, CharacterHealth.LimbHealth limbHealth, CharacterHealth selfHealth, List<CharacterHealth.LimbHealth> limbHealths)
            {
                if (!kvp.Key.ShouldShowIcon(selfHealth.Character)) { return false; }
                if (kvp.Value == limbHealth)
                {
                    return true;
                }
                else if (kvp.Value == null)
                {
                    Limb indicatorLimb = selfHealth.Character.AnimController.GetLimb(kvp.Key.Prefab.IndicatorLimb);
                    return indicatorLimb != null && indicatorLimb.HealthIndex == limbHealths.IndexOf(limbHealth);
                }
                return false;
            }

            // CreateAfflictionInfos override
            // makes it so the affliction descriptions are next to each other if buffs are present
            // also makes them smaller so theres room for more of them
            const int displayedAfflictionCountMax = 8;
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_CreateAfflictionInfos",
                typeof(CharacterHealth).GetMethod("CreateAfflictionInfos", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    CreateAfflictionInfos(self, args);
                    return true;
                }, LuaCsHook.HookMethodType.Before, this);
            void CreateAfflictionInfos(object self, Dictionary<string, object> args)
            {
                ForceCustomized((CharacterHealth)self);

                #region Reflection crap
                // get arguments
                IEnumerable<Affliction> afflictions = (IEnumerable<Affliction>)(args["afflictions"]);
                // get members
                GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                GUIListBox afflictionIconContainer2 = (GUIListBox)(afflictionIconContainer.Parent.GetChildByUserData("afflictionIconContainer2"));
                List<(Affliction affliction, float strength)> displayedAfflictions = (List<(Affliction affliction, float strength)>)(typeof(CharacterHealth).GetField("displayedAfflictions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                #endregion
                // override begins
                afflictionIconContainer.ClearChildren();
                afflictionIconContainer2.ClearChildren();
                displayedAfflictions.Clear();

                afflictions = CharacterHealth.SortAfflictionsBySeverity(afflictions, false);
                Affliction mostSevereAffliction = afflictions.FirstOrDefault();
                GUIButton buttonToSelect = null;

                foreach (Affliction affliction in afflictions)
                {
                    displayedAfflictions.Add((affliction, affliction.Strength));

                    bool isBuff = affliction.Prefab.IsBuff;
                    var newParent = (isBuff ? afflictionIconContainer2 : afflictionIconContainer);

                    var frame = new GUIButton(new RectTransform(new Vector2(1.0f, 1f / displayedAfflictionCountMax),newParent.Content.RectTransform), style: "ListBoxElement")
                    {
                        UserData = affliction,
                        OnClicked = SelectAffliction
                    };

                    new GUIFrame(new RectTransform(Vector2.One, frame.RectTransform), style: "GUIFrameListBox") { CanBeFocused = false };

                    // houses the progress bar
                    var content = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.75f), frame.RectTransform, Anchor.Center), childAnchor: Anchor.CenterLeft)
                    {
                        Stretch = true,
                        CanBeFocused = false,
                        IsHorizontal = true
                    };
                    // spacing
                    new GUIFrame(new RectTransform(new Vector2(0.1f, 1f), content.RectTransform), style: "GUIFrameListBox") { CanBeFocused = false };

                    // houses the affliction icon and text
                    var content2 = new GUILayoutGroup(new RectTransform(new Vector2(1f, 0.95f), frame.RectTransform, Anchor.Center), childAnchor: Anchor.CenterLeft)
                    {
                        Stretch = true,
                        CanBeFocused = false,
                        IsHorizontal = true
                    };

                    var progressbarBg = new GUIProgressBar(new RectTransform(new Vector2(0.5f, 1), content.RectTransform), 0.0f, GUIStyle.Green, style: "GUIAfflictionBar")
                    {
                        UserData = "afflictionstrengthprediction",
                        CanBeFocused = false,
                        IsHorizontal = true
                    };
                    var afflictionStrengthBar = new GUIProgressBar(new RectTransform(Vector2.One, progressbarBg.RectTransform), 0.0f, Color.Transparent, showFrame: false, style: "GUIAfflictionBar")
                    {
                        UserData = "afflictionstrength",
                        CanBeFocused = false,
                        IsHorizontal = true
                    };
                    afflictionStrengthBar.BarSize = affliction.Strength / affliction.Prefab.MaxStrength;

                    //spacing
                    //new GUIFrame(new RectTransform(new Vector2(1.0f, 0.15f), content.RectTransform), style: null) { CanBeFocused = false };

                    if (affliction == mostSevereAffliction)
                    {
                        buttonToSelect = frame;
                    }

                    var afflictionIcon = new GUIImage(new RectTransform(Vector2.One * 1f, content2.RectTransform, anchor: Anchor.CenterLeft, pivot: Pivot.CenterLeft, scaleBasis: ScaleBasis.BothHeight), affliction.Prefab.Icon, scaleToFit: true)
                    {
                        Color = CharacterHealth.GetAfflictionIconColor(affliction),
                        CanBeFocused = false
                    };
                    afflictionIcon.PressedColor = afflictionIcon.Color;
                    afflictionIcon.HoverColor = Color.Lerp(afflictionIcon.Color, Color.White, 0.6f);
                    afflictionIcon.SelectedColor = Color.Lerp(afflictionIcon.Color, Color.White, 0.5f);

                    var nameText = new GUITextBlock(new RectTransform(new Vector2(1.1f, 0.0f), content2.RectTransform),
                        $"{affliction.Prefab.Name}\n({Math.Round(affliction.Strength / affliction.Prefab.MaxStrength * 100)}% | {Math.Round(affliction.Strength)}/{Math.Round(affliction.Prefab.MaxStrength)})", font: GUIStyle.SmallFont, textAlignment: Alignment.CenterLeft)
                    {
                        UserData = "afflictionname",
                        CanBeFocused = false,
                        OutlineColor = Color.Black,
                        Shadow = true
                    };
                    nameText.Text = ToolBox.LimitString(nameText.Text, nameText.Font, nameText.Rect.Width);
                    nameText.RectTransform.MinSize = new Point(0, (int)(nameText.TextSize.Y));
                    nameText.RectTransform.SizeChanged += () =>
                    {
                        nameText.Text = ToolBox.LimitString(nameText.Text, nameText.Font, nameText.Rect.Width);
                    };

                    content.Recalculate();
                    content2.Recalculate();
                }

                buttonToSelect?.OnClicked(buttonToSelect, buttonToSelect.UserData);
                afflictionIconContainer.RecalculateChildren();
                afflictionIconContainer.ForceLayoutRecalculation();
                afflictionIconContainer2.RecalculateChildren();
                afflictionIconContainer2.ForceLayoutRecalculation();

                bool SelectAffliction(GUIButton button, object userData)
                {
                    bool selected = button.Selected;
                    foreach (var child in afflictionIconContainer.Content.Children)
                    {
                        GUIButton btn = child.GetChild<GUIButton>();
                        if (btn != null)
                        {
                            btn.Selected = btn == button && !selected;
                        }
                    }
                    foreach (var child in afflictionIconContainer2.Content.Children)
                    {
                        GUIButton btn = child.GetChild<GUIButton>();
                        if (btn != null)
                        {
                            btn.Selected = btn == button && !selected;
                        }
                    }

                    return false;
                }
            }

            // UpdateAfflictionContainer override
            // got some custom double list crap going on here
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_UpdateAfflictionContainer",
                typeof(CharacterHealth).GetMethod("UpdateAfflictionContainer", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    UpdateAfflictionContainer((CharacterHealth.LimbHealth)(args["selectedLimb"]), (CharacterHealth)self);
                    return true;
                }, LuaCsHook.HookMethodType.Before, this);
            void UpdateAfflictionContainer(CharacterHealth.LimbHealth selectedLimb,CharacterHealth selfHealth)
            {
                ForceCustomized(selfHealth);
                GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                GUIListBox afflictionIconContainer2 = (GUIListBox)(afflictionIconContainer.Parent.GetChildByUserData("afflictionIconContainer2"));
                List<(Affliction affliction, float strength)> displayedAfflictions = (List<(Affliction affliction, float strength)>)(typeof(CharacterHealth).GetField("displayedAfflictions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                Dictionary<Affliction, CharacterHealth.LimbHealth> afflictions = (Dictionary<Affliction, CharacterHealth.LimbHealth>)(typeof(CharacterHealth).GetField("afflictions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                CharacterHealth.LimbHealth currentDisplayedLimb = (CharacterHealth.LimbHealth)(typeof(CharacterHealth).GetField("currentDisplayedLimb", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                List<CharacterHealth.LimbHealth> limbHealths = (List<CharacterHealth.LimbHealth>)(typeof(CharacterHealth).GetField("limbHealths", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));

                if (selectedLimb == null)
                {
                    afflictionIconContainer.Content.ClearChildren();
                    afflictionIconContainer2.Content.ClearChildren();
                    return;
                }

                if (afflictionsDirty() || selectedLimb != currentDisplayedLimb)
                {
                    var currentAfflictions = afflictions.Where(a => ShouldDisplayAfflictionOnLimb(a, selectedLimb,selfHealth,limbHealths)).Select(a => a.Key);
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add("afflictions", currentAfflictions);
                    CreateAfflictionInfos(selfHealth, args);
                    CreateRecommendedTreatments();
                }
                //update recommended treatments if the strength of some displayed affliction has changed by > 1
                else if (displayedAfflictions.Any(d => Math.Abs(d.strength - d.affliction.Strength) > 1.0f))
                {
                    CreateRecommendedTreatments();
                }

                bool afflictionsDirty()
                {
                    //not displaying one of the current afflictions -> dirty
                    foreach (KeyValuePair<Affliction, CharacterHealth.LimbHealth> kvp in afflictions)
                    {
                        if (!ShouldDisplayAfflictionOnLimb(kvp, selectedLimb,selfHealth,limbHealths)) { continue; }
                        if (!displayedAfflictions.Any(d => d.affliction == kvp.Key)) { return true; }
                    }
                    //displaying an affliction we no longer have -> dirty
                    foreach ((Affliction affliction, float strength) in displayedAfflictions)
                    {
                        if (!afflictions.Any(a => a.Key == affliction)) { return true; }
                    }
                    return false;
                }

                void CreateRecommendedTreatments()
                {
                    typeof(CharacterHealth).GetMethod("CreateRecommendedTreatments", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selfHealth, null);
                }
            }

            // UpdateAfflictionInfos override
            // makes it so the affliction progress bar is nicely colored
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_UpdateAfflictionInfos",
                typeof(CharacterHealth).GetMethod("UpdateAfflictionInfos", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    try
                    {
                        #region Reflection crap
                        // get arguments
                        IEnumerable<Affliction> afflictions = (IEnumerable<Affliction>)(args["afflictions"]);
                        // get members
                        CharacterHealth selfHealth = (CharacterHealth)self;
                        ForceCustomized(selfHealth);
                        GUIListBox afflictionIconContainer = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionIconList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                        GUIListBox afflictionIconContainer2 = (GUIListBox)(afflictionIconContainer.Parent.GetChildByUserData("afflictionIconContainer2"));
                        GUIListBox afflictionTooltip = (GUIListBox)(typeof(CharacterHealth).GetField("afflictionTooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self));
                        //LocalizedString[] strengthTexts = (LocalizedString[])(typeof(CharacterHealth).GetField("strengthTexts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(self));
                        #endregion
                        // override begins

                        var potentialTreatment = Inventory.DraggingItems.FirstOrDefault();
                        if (potentialTreatment == null && GUI.MouseOn?.UserData is ItemPrefab itemPrefab)
                        {
                            potentialTreatment = Character.Controlled.Inventory.FindItem(it => it.Prefab == itemPrefab, recursive: true);
                        }
                        potentialTreatment ??= Inventory.SelectedSlot?.Item;

                        foreach (Affliction affliction in afflictions)
                        {
                            float afflictionVitalityDecrease = affliction.GetVitalityDecrease(selfHealth);
                            Color afflictionEffectColor = Color.Lerp(CharacterHealth.GetAfflictionIconColor(affliction), Color.Black, 0.5f);

                            bool isBuff = affliction.Prefab.IsBuff;

                            var child = (isBuff ? afflictionIconContainer2 : afflictionIconContainer).Content.FindChild(affliction);

                            var afflictionName = child.FindChild("afflictionname", true) as GUITextBlock;
                            afflictionName.Text = $"{affliction.Prefab.Name}\n({Math.Round(affliction.Strength / affliction.Prefab.MaxStrength * 100)}% | {Math.Round(affliction.Strength)}/{Math.Round(affliction.Prefab.MaxStrength)})";

                            var afflictionStrengthPredictionBar = child.FindChild("afflictionstrengthprediction", true) as GUIProgressBar;
                            afflictionStrengthPredictionBar.BarSize = 0.0f;
                            var afflictionStrengthBar = afflictionStrengthPredictionBar.GetChildByUserData("afflictionstrength") as GUIProgressBar;
                            afflictionStrengthBar.BarSize = MathHelper.Lerp(afflictionStrengthBar.BarSize, affliction.Strength / affliction.Prefab.MaxStrength, 0.1f);
                            afflictionStrengthBar.Color = afflictionEffectColor;

                            float afflictionStrengthPrediction = GetAfflictionStrengthPrediction(potentialTreatment, affliction);
                            if (!MathUtils.NearlyEqual(afflictionStrengthPrediction, affliction.Strength))
                            {
                                float t = (float)Math.Max(0.5f, (Math.Sin(Timing.TotalTime * 5) + 1.0f) / 2.0f);
                                if (afflictionStrengthPrediction < affliction.Strength)
                                {
                                    afflictionStrengthBar.Color = afflictionEffectColor;
                                    afflictionStrengthPredictionBar.Color = GUIStyle.Blue * t;
                                    afflictionStrengthPredictionBar.BarSize = afflictionStrengthBar.BarSize;
                                    afflictionStrengthBar.BarSize = afflictionStrengthPrediction / affliction.Prefab.MaxStrength;
                                }
                                else
                                {
                                    afflictionStrengthPredictionBar.Color = Color.Red * t;
                                    afflictionStrengthPredictionBar.BarSize = afflictionStrengthPrediction / affliction.Prefab.MaxStrength;
                                }
                            }

                            if (afflictionTooltip != null && afflictionTooltip.UserData == affliction)
                            {
                                UpdateAfflictionInfo(afflictionTooltip.Content, affliction);
                            }
                        }

                        void UpdateAfflictionInfo(GUIComponent parent, Affliction affliction)
                        {
                            var labelContainer = parent.GetChildByUserData("label");

                            var strengthText = labelContainer.GetChildByUserData("strength") as GUITextBlock;

                            strengthText.Text = affliction.GetStrengthText();

                            strengthText.TextColor = Color.Lerp(GUIStyle.Orange, GUIStyle.Red,
                                affliction.Strength / affliction.Prefab.MaxStrength);

                            var vitalityText = labelContainer.GetChildByUserData("vitality") as GUITextBlock;
                            int vitalityDecrease = (int)affliction.GetVitalityDecrease(selfHealth);
                            if (vitalityDecrease == 0)
                            {
                                vitalityText.Visible = false;
                            }
                            else
                            {
                                vitalityText.Visible = true;
                                vitalityText.Text = TextManager.Get("Vitality") + " -" + vitalityDecrease;
                                vitalityText.TextColor = vitalityDecrease <= 0 ? GUIStyle.Green :
                                Color.Lerp(GUIStyle.Orange, GUIStyle.Red, affliction.Strength / affliction.Prefab.MaxStrength);
                            }
                        }

                        float GetAfflictionStrengthPrediction(Item item, Affliction affliction)
                        {
                            float strength = affliction.Strength;
                            if (item == null) { return strength; }

                            foreach (ItemComponent ic in item.Components)
                            {
                                if (ic.statusEffectLists == null) { continue; }
                                if (!ic.statusEffectLists.TryGetValue(ActionType.OnUse, out List<StatusEffect> statusEffects)) { continue; }
                                foreach (StatusEffect effect in statusEffects)
                                {
                                    foreach (var reduceAffliction in effect.ReduceAffliction)
                                    {
                                        if (reduceAffliction.AfflictionIdentifier != affliction.Identifier && reduceAffliction.AfflictionIdentifier != affliction.Prefab.AfflictionType) { continue; }
                                        strength -= reduceAffliction.ReduceAmount * (effect.Duration > 0 ? effect.Duration : 1.0f);
                                    }
                                    foreach (var addAffliction in effect.Afflictions)
                                    {
                                        if (addAffliction.Prefab != affliction.Prefab) { continue; }
                                        strength += addAffliction.Strength * (effect.Duration > 0 ? effect.Duration : 1.0f);
                                    }
                                }
                            }
                            return strength;
                        }

                        healthGUIRefreshTimer--;
                        if (healthGUIRefreshTimer <= 0)
                        {
                            healthGUIRefreshTimer = 60;
                            afflictionIconContainer.Content.RectTransform.SortChildren((r1, r2) =>
                            {
                                var first = r1.GUIComponent.UserData as Affliction;
                                var second = r2.GUIComponent.UserData as Affliction;
                                return Math.Sign(second.Strength / second.Prefab.MaxStrength - first.Strength / first.Prefab.MaxStrength);
                            });
                            afflictionIconContainer2.Content.RectTransform.SortChildren((r1, r2) =>
                            {
                                var first = r1.GUIComponent.UserData as Affliction;
                                var second = r2.GUIComponent.UserData as Affliction;
                                return Math.Sign(second.Strength / second.Prefab.MaxStrength - first.Strength / first.Prefab.MaxStrength);
                            });
                        }

                    }
                    catch (Exception e)
                    {
                        LuaCsSetup.PrintCsMessage("shit. " + e);
                    }
                    

                    

                    return true;
                }, LuaCsHook.HookMethodType.Before, this);

            // UpdateLimbIndicators override
            // makes it so i can move the funny limb man
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_UpdateLimbIndicators",
                typeof(CharacterHealth).GetMethod("UpdateLimbIndicators", BindingFlags.Instance | BindingFlags.NonPublic),
                (object self, Dictionary<string, object> args) => {
                    // get arguments
                    float deltaTime = (float)(args["deltaTime"]);
                    Rectangle drawArea = (Rectangle)(args["drawArea"]);

                    CharacterHealth selfHealth = (CharacterHealth)self;

                    UpdateLimbIndicators(deltaTime, drawArea, selfHealth);

                    return true;
                }, LuaCsHook.HookMethodType.Before, this);
            void UpdateLimbIndicators(float deltaTime, Rectangle drawArea, CharacterHealth selfHealth)
            {
                // get members
                float limbIndicatorOverlayAnimState = (float)(typeof(CharacterHealth).GetField("limbIndicatorOverlayAnimState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                int highlightedLimbIndex = (int)(typeof(CharacterHealth).GetField("highlightedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                int selectedLimbIndex = (int)(typeof(CharacterHealth).GetField("selectedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                List<CharacterHealth.LimbHealth> limbHealths = (List<CharacterHealth.LimbHealth>)(typeof(CharacterHealth).GetField("limbHealths", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selfHealth));
                // override begins

                if (!GameMain.Instance.Paused)
                {
                    limbIndicatorOverlayAnimState += deltaTime * 8.0f;
                    // reflection apply
                    typeof(CharacterHealth).GetField("limbIndicatorOverlayAnimState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(selfHealth, limbIndicatorOverlayAnimState);
                }

                highlightedLimbIndex = -1;
                int i = 0;
                foreach (CharacterHealth.LimbHealth limbHealth in limbHealths)
                {
                    if (limbHealth.IndicatorSprite == null) { continue; }

                    float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);

                    Rectangle highlightArea = GetLimbHighlightArea(limbHealth, drawArea, selfHealth);

                    if (highlightArea.Contains(PlayerInput.MousePosition))
                    {
                        highlightedLimbIndex = i;
                    }
                    i++;
                }
                // reflection apply
                typeof(CharacterHealth).GetField("highlightedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(selfHealth, highlightedLimbIndex);

                if (PlayerInput.PrimaryMouseButtonClicked() && highlightedLimbIndex > -1)
                {
                    selectedLimbIndex = highlightedLimbIndex;
                    // reflection apply
                    typeof(CharacterHealth).GetField("selectedLimbIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(selfHealth, selectedLimbIndex);
                }
            }

            // SortAfflictionsBySeverity override
            // makes it so the afflictions dont spazz around
            GameMain.LuaCs.Hook.HookMethod("BetterHealthUIMod_SortAfflictionsBySeverity",
                typeof(CharacterHealth).GetMethod("SortAfflictionsBySeverity", BindingFlags.Static | BindingFlags.Public),
                (object self, Dictionary<string, object> args) => {
                    #region Reflection crap
                    // get arguments
                    IEnumerable<Affliction> afflictions = (IEnumerable<Affliction>)(args["afflictions"]);
                    bool excludeBuffs = (bool)(args["excludeBuffs"]);
                    #endregion
                    // override begins
                    return afflictions.Where(a => !excludeBuffs || !a.Prefab.IsBuff).OrderByDescending(a => a.Strength / a.Prefab.MaxStrength);
                }, LuaCsHook.HookMethodType.Before, this);

            float GetTotalDamage(CharacterHealth.LimbHealth limbHealth, Dictionary<Affliction, CharacterHealth.LimbHealth> afflictions,CharacterHealth self)
            {
                float totalDamage = 0.0f;
                foreach (KeyValuePair<Affliction, CharacterHealth.LimbHealth> kvp in afflictions)
                {
                    if (kvp.Value != limbHealth) { continue; }
                    var affliction = kvp.Key;
                    totalDamage += affliction.GetVitalityDecrease(self);
                }
                return totalDamage;
            }

            Rectangle GetLimbHighlightArea(CharacterHealth.LimbHealth limbHealth, Rectangle drawArea, CharacterHealth selfHealth)
            {
                float scale = Math.Min(drawArea.Width / (float)limbHealth.IndicatorSprite.SourceRect.Width, drawArea.Height / (float)limbHealth.IndicatorSprite.SourceRect.Height);
                return new Rectangle(
                    (int)(drawArea.Center.X + limbGuyOffset.X - (limbHealth.IndicatorSprite.SourceRect.Width / 2 - limbHealth.HighlightArea.X) * scale),
                    (int)(drawArea.Center.Y + limbGuyOffset.Y - (limbHealth.IndicatorSprite.SourceRect.Height / 2 - limbHealth.HighlightArea.Y) * scale),
                    (int)(limbHealth.HighlightArea.Width * scale),
                    (int)(limbHealth.HighlightArea.Height * scale));
            }

            Color AverageColor(List<object[]> colors)
            {
                float num = 0;
                float r = 0;
                float g = 0;
                float b = 0;

                foreach (object[] colarr in colors)
                {
                    Color col = (Color)colarr[0];
                    float strength = (float)colarr[1];
                    r += col.R * col.R * strength;
                    g += col.G * col.G * strength;
                    b += col.B * col.B * strength;
                    num += strength;
                }

                return new Color((float)Math.Sqrt(r / num)/255f, (float)Math.Sqrt(g / num) / 255f, (float)Math.Sqrt(b / num) / 255f);
            }

            LimbType NormalizeLimbType(LimbType type)
            {
                switch (type)
                {
                    case LimbType.Waist: return LimbType.Torso;
                    case LimbType.LeftHand:
                    case LimbType.LeftForearm:
                        return LimbType.LeftArm;
                    case LimbType.RightHand:
                    case LimbType.RightForearm:
                        return LimbType.RightArm;
                    case LimbType.LeftFoot:
                    case LimbType.LeftThigh:
                        return LimbType.LeftLeg;
                    case LimbType.RightFoot:
                    case LimbType.RightThigh:
                        return LimbType.RightLeg;
                }

                return type;
            }

            LimbType LimbHealthToLimbType(CharacterHealth.LimbHealth limbHealth, List<CharacterHealth.LimbHealth>  limbHealths, Limb[] Limbs)
            {
                int healthIndex = limbHealths.IndexOf(limbHealth);
                Limb l = Limbs.Find(l => l.HealthIndex == healthIndex);
                if (l == null) return LimbType.None;
                return l.type;
            }

        }

        // update heartrate monitor (Neurotrauma)
        const int GraphSize = 256;
        const float updateGraphInterval = 1 / 60f; // update at 60 fps
        const float NormalHeartrate = 60;
        const float MaxTachycardiaHeartrate = 180;
        const float MaxFibrillationHeartrate = 300;

        private static float graphTimer = 0;
        private static float[] heartGraph = new float[GraphSize];
        private static int heartGraphProgress = 0;
        private static float timeSinceBeat = 0;
        private static CCurve ecgCurve = null;
        private static CCurve ecgCurveFib = null;
        private static SoundChannel flatlineSoundChannel = null;

        private static void UpdateGraph()
        {
            const float deltaTime = 1 / 60f;

            graphTimer += deltaTime;
            timeSinceBeat += deltaTime;

            if (graphTimer >= updateGraphInterval)
            {
                Character character = CharacterHealth.OpenHealthWindow?.Character;
                UpdateHeartrateGraphData(heartGraph, GetHeartbeatAmplitude(character)+1);

                graphTimer = 0.0f;
            }
        }
        private static float GetHeartbeatAmplitude(Character character)
        {
            if (character == null) return 0.0f;

            (float rate, float stability) heartrate = GetHeartrate(character);
            float chaos = MathHelper.Lerp(1,rnd.Next(0, 1000) / 1000f, Math.Min(1,(1-heartrate.stability)*2));

            float timePerBeat = heartrate.rate > 0 ? 1 / (heartrate.rate / 60 * (1 + (1 - heartrate.stability) * 2 * (rnd.Next(0, 1000) / 1000f))) : float.PositiveInfinity;

            // play flatline sound
            if (heartrate.rate <= 0)
            {
                if (flatlineSoundChannel == null)
                {
                    flatlineSoundChannel = SoundPlayer.PlaySound("flatline2",0.06f);
                    // checking if we actually got a channel first (if for some reason we dont, then this would throw an error (bad))
                    if(flatlineSoundChannel!=null) flatlineSoundChannel.Looping = true;
                }

                return 0;
            }
            
            // stop playing flatline sound if patient recovered
            if (flatlineSoundChannel != null) 
            { 
                flatlineSoundChannel.Dispose();
                flatlineSoundChannel = null;
            }

            // start playing the ECG beep sound
            if (timeSinceBeat > timePerBeat)
            {
                timeSinceBeat -= timePerBeat;

                if(heartrate.rate > NormalHeartrate+10 && rnd.Next(0, 1000) / 1000f < heartrate.stability)
                SoundPlayer.PlaySound("ecg1",0.1f*Math.Min((heartrate.rate-NormalHeartrate)/80, 1));

            }

            return MathHelper.Lerp(ecgCurve.Evaluate(timeSinceBeat*1.5f)* chaos, ecgCurveFib.Evaluate(timeSinceBeat*1.5f),1- heartrate.stability);
        }
        private static (float rate,float stability) GetHeartrate(Character character)
        {
            if (character == null || character.CharacterHealth==null || character.IsDead) return (0,0);

            float rate = NormalHeartrate;
            float stability = 1;

            Affliction cardiacarrest = character.CharacterHealth.GetAffliction("cardiacarrest");

            // return 0 rate and stability if in cardiac arrest
            if (cardiacarrest != null && cardiacarrest.Strength >= 0.5f) return (0,0);

            Affliction tachycardia = character.CharacterHealth.GetAffliction("tachycardia");
            Affliction fibrillation = character.CharacterHealth.GetAffliction("fibrillation");

            if (fibrillation!=null)
            { 
                rate = MathHelper.Lerp(MaxTachycardiaHeartrate, MaxFibrillationHeartrate, fibrillation.Strength/100 * (0.25f+(rnd.Next(0, 1000) / 1000f)));
                stability = 1 - fibrillation.Strength / 100;
            }
            else if (tachycardia != null)
            {
                rate = MathHelper.Lerp(NormalHeartrate, MaxTachycardiaHeartrate, tachycardia.Strength / 100);
            }

            return (rate,stability);
        }

        private static void UpdateHeartrateGraphData(IList<float> graph, float newValue)
        {
            graph[heartGraphProgress] = newValue*0.8f;
            heartGraphProgress = (heartGraphProgress + 1) % graph.Count;
        }
    }

    // i hate this
    // trying to use the already existing curve class throws an error that its in two assemblies
    // i dont know how to fix it so here comes the duplicate classes!

    public enum CCurveLoopType
    {
        Constant,
        Cycle,
        CycleOffset,
        Oscillate,
        Linear
    }

    public enum CCurveContinuity
    {
        Smooth,
        Step
    }

    public enum CCurveTangent
    {
        Flat,
        Linear,
        Smooth
    }

    public class CCurve
    {
        #region Private Fields

        private CCurveKeyCollection keys;
        private CCurveLoopType postLoop;
        private CCurveLoopType preLoop;

        #endregion Private Fields

        #region Public Properties

        public bool IsConstant
        {
            get { return keys.Count <= 1; }
        }

        public CCurveKeyCollection Keys
        {
            get { return keys; }
        }

        public CCurveLoopType PostLoop
        {
            get { return postLoop; }
            set { postLoop = value; }
        }

        public CCurveLoopType PreLoop
        {
            get { return preLoop; }
            set { preLoop = value; }
        }

        #endregion Public Properties

        #region Public Constructors

        public CCurve()
        {
            keys = new CCurveKeyCollection();
        }

        #endregion Public Constructors

        #region Public Methods

        public CCurve Clone()
        {
            CCurve curve = new CCurve();

            curve.keys = keys.Clone();
            curve.preLoop = preLoop;
            curve.postLoop = postLoop;

            return curve;
        }

        public float Evaluate(float position)
        {
            CCurveKey first = keys[0];
            CCurveKey last = keys[keys.Count - 1];

            if (position < first.Position)
            {
                switch (PreLoop)
                {
                    case CCurveLoopType.Constant:
                        //constant
                        return first.Value;

                    case CCurveLoopType.Linear:
                        // linear y = a*x +b with a tangeant of last point
                        return first.Value - first.TangentIn * (first.Position - position);

                    case CCurveLoopType.Cycle:
                        //start -> end / start -> end
                        int cycle = GetNumberOfCycle(position);
                        float virtualPos = position - (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);

                    case CCurveLoopType.CycleOffset:
                        //make the curve continue (with no step) so must up the curve each cycle of delta(value)
                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));
                        return (GetCurvePosition(virtualPos) + cycle * (last.Value - first.Value));

                    case CCurveLoopType.Oscillate:
                        //go back on curve from end and target start 
                        // start-> end / end -> start
                        cycle = GetNumberOfCycle(position);
                        if (0 == cycle % 2f) //if pair
                            virtualPos = position - (cycle * (last.Position - first.Position));
                        else
                            virtualPos = last.Position - position + first.Position +
                                            (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);
                }
            }
            else if (position > last.Position)
            {
                int cycle;
                switch (PostLoop)
                {
                    case CCurveLoopType.Constant:
                        //constant
                        return last.Value;

                    case CCurveLoopType.Linear:
                        // linear y = a*x +b with a tangeant of last point
                        return last.Value + first.TangentOut * (position - last.Position);

                    case CCurveLoopType.Cycle:
                        //start -> end / start -> end
                        cycle = GetNumberOfCycle(position);
                        float virtualPos = position - (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);

                    case CCurveLoopType.CycleOffset:
                        //make the curve continue (with no step) so must up the curve each cycle of delta(value)
                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));
                        return (GetCurvePosition(virtualPos) + cycle * (last.Value - first.Value));

                    case CCurveLoopType.Oscillate:
                        //go back on curve from end and target start 
                        // start-> end / end -> start
                        cycle = GetNumberOfCycle(position);
                        virtualPos = position - (cycle * (last.Position - first.Position));
                        if (0 == cycle % 2f) //if pair
                            virtualPos = position - (cycle * (last.Position - first.Position));
                        else
                            virtualPos = last.Position - position + first.Position +
                                            (cycle * (last.Position - first.Position));
                        return GetCurvePosition(virtualPos);
                }
            }

            //in curve
            return GetCurvePosition(position);
        }

        #endregion Public Methods

        #region Private Methods

        private int GetNumberOfCycle(float position)
        {
            float cycle = (position - keys[0].Position) / (keys[keys.Count - 1].Position - keys[0].Position);
            if (cycle < 0f)
                cycle--;
            return (int)cycle;
        }

        private float GetCurvePosition(float position)
        {
            //only for position in curve
            CCurveKey prev = keys[0];
            CCurveKey next;
            for (int i = 1; i < keys.Count; i++)
            {
                next = Keys[i];
                if (next.Position >= position)
                {
                    if (prev.Continuity == CCurveContinuity.Step)
                    {
                        if (position >= 1f)
                        {
                            return next.Value;
                        }
                        return prev.Value;
                    }
                    float t = (position - prev.Position) / (next.Position - prev.Position); //to have t in [0,1]
                    float ts = t * t;
                    float tss = ts * t;
                    //After a lot of search on internet I have found all about spline function
                    // and bezier (phi'sss ancien) but finaly use hermite curve 
                    //http://en.wikipedia.org/wiki/Cubic_Hermite_spline
                    //P(t) = (2*t^3 - 3t^2 + 1)*P0 + (t^3 - 2t^2 + t)m0 + (-2t^3 + 3t^2)P1 + (t^3-t^2)m1
                    //with P0.value = prev.value , m0 = prev.tangentOut, P1= next.value, m1 = next.TangentIn
                    return (2 * tss - 3 * ts + 1f) * prev.Value + (tss - 2 * ts + t) * prev.TangentOut + (3 * ts - 2 * tss) * next.Value +
                            (tss - ts) * next.TangentIn;
                }
                prev = next;
            }
            return 0f;
        }

        #endregion
    }

    public class CCurveKey : IEquatable<CCurveKey>, IComparable<CCurveKey>
    {
        #region Private Fields

        private CCurveContinuity continuity;
        private float position;
        private float tangentIn;
        private float tangentOut;
        private float value;

        #endregion Private Fields

        #region Properties

        public CCurveContinuity Continuity
        {
            get { return continuity; }
            set { continuity = value; }
        }

        public float Position
        {
            get { return position; }
        }

        public float TangentIn
        {
            get { return tangentIn; }
            set { tangentIn = value; }
        }

        public float TangentOut
        {
            get { return tangentOut; }
            set { tangentOut = value; }
        }

        public float Value
        {
            get { return value; }
            set { this.value = value; }
        }

        #endregion

        #region Constructors

        public CCurveKey(float position, float value)
            : this(position, value, 0, 0, CCurveContinuity.Smooth)
        {
        }

        public CCurveKey(float position, float value, float tangentIn, float tangentOut)
            : this(position, value, tangentIn, tangentOut, CCurveContinuity.Smooth)
        {
        }

        public CCurveKey(float position, float value, float tangentIn, float tangentOut, CCurveContinuity continuity)
        {
            this.position = position;
            this.value = value;
            this.tangentIn = tangentIn;
            this.tangentOut = tangentOut;
            this.continuity = continuity;
        }

        #endregion Constructors

        #region Public Methods

        #region IComparable<CurveKey> Members

        public int CompareTo(CCurveKey other)
        {
            return position.CompareTo(other.position);
        }

        #endregion

        #region IEquatable<CCurveKey> Members

        public bool Equals(CCurveKey other)
        {
            return (this == other);
        }

        #endregion

        public static bool operator !=(CCurveKey a, CCurveKey b)
        {
            return !(a == b);
        }

        public static bool operator ==(CCurveKey a, CCurveKey b)
        {
            if (Equals(a, null))
                return Equals(b, null);

            if (Equals(b, null))
                return Equals(a, null);

            return (a.position == b.position)
                   && (a.value == b.value)
                   && (a.tangentIn == b.tangentIn)
                   && (a.tangentOut == b.tangentOut)
                   && (a.continuity == b.continuity);
        }

        public CCurveKey Clone()
        {
            return new CCurveKey(position, value, tangentIn, tangentOut, continuity);
        }

        public override bool Equals(object obj)
        {
            return (obj is CCurveKey) ? ((CCurveKey)obj) == this : false;
        }

        public override int GetHashCode()
        {
            return position.GetHashCode() ^ value.GetHashCode() ^ tangentIn.GetHashCode() ^
                   tangentOut.GetHashCode() ^ continuity.GetHashCode();
        }

        #endregion
    }

    public class CCurveKeyCollection
    {
        #region Private Fields

        private List<CCurveKey> innerlist;
        private bool isReadOnly = false;

        #endregion Private Fields

        #region Properties

        public CCurveKey this[int index]
        {
            get { return innerlist[index]; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                if (index >= innerlist.Count)
                    throw new IndexOutOfRangeException();

                if (innerlist[index].Position == value.Position)
                    innerlist[index] = value;
                else
                {
                    innerlist.RemoveAt(index);
                    innerlist.Add(value);
                }
            }
        }

        public int Count
        {
            get { return innerlist.Count; }
        }

        public bool IsReadOnly
        {
            get { return isReadOnly; }
        }

        #endregion Properties

        #region Constructors

        public CCurveKeyCollection()
        {
            innerlist = new List<CCurveKey>();
        }

        #endregion Constructors

        #region Public Methods

        public void Add(CCurveKey item)
        {
            if (item == null)
                throw new ArgumentNullException("Value cannot be null.", (Exception)null);

            if (innerlist.Count == 0)
            {
                innerlist.Add(item);
                return;
            }

            for (int i = 0; i < innerlist.Count; i++)
            {
                if (item.Position < innerlist[i].Position)
                {
                    innerlist.Insert(i, item);
                    return;
                }
            }

            innerlist.Add(item);
        }

        public void Clear()
        {
            innerlist.Clear();
        }

        public bool Contains(CCurveKey item)
        {
            return innerlist.Contains(item);
        }

        public void CopyTo(CCurveKey[] array, int arrayIndex)
        {
            innerlist.CopyTo(array, arrayIndex);
        }

        public IEnumerator<CCurveKey> GetEnumerator()
        {
            return innerlist.GetEnumerator();
        }

        public bool Remove(CCurveKey item)
        {
            return innerlist.Remove(item);
        }



        public CCurveKeyCollection Clone()
        {
            CCurveKeyCollection ckc = new CCurveKeyCollection();
            foreach (CCurveKey key in innerlist)
                ckc.Add(key);
            return ckc;
        }

        public int IndexOf(CCurveKey item)
        {
            return innerlist.IndexOf(item);
        }

        public void RemoveAt(int index)
        {
            if (index != Count && index > -1)
                innerlist.RemoveAt(index);
            else
                throw new ArgumentOutOfRangeException(
                    "Index was out of range. Must be non-negative and less than the size of the collection.\r\nParameter name: index",
                    (Exception)null);
        }

        #endregion Public Methods
    }
}