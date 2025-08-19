using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ClickableTransparentOverlay;
using ImGuiNET;
using System.Windows;
using System.ComponentModel.Design;

namespace eclipse_external
{
    public class Renderer : Overlay
    {
        // render variables
        public Vector2 screenSize = new Vector2(1920, 1080); // разрешение ваше игровое

        // safe method
        private ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        // Gui Element
        private bool enableESP = true;
        private bool enableLine = false;
        private bool enableBones = true;
        private bool enableFr = true;
        private bool enableHealthInfo = true;
        private Vector4 enemyColor = new Vector4(1, 0, 0, 1); // red 
        private Vector4 teamColor = new Vector4(0, 1, 0, 1); // green
        private Vector4 boneColor = new Vector4(1, 1, 1, 1); // rgba white

        float boneThicksness = 4;

        // draw list
        ImDrawListPtr drawList;

        protected override void Render()
        {
            // ImGui Menu

            ImGui.Begin("eclispe");
            ImGui.Checkbox("Enable ESP", ref enableESP);
            ImGui.Checkbox("Enable Line", ref enableLine);
            ImGui.Checkbox("Enable Bones", ref enableBones);
            ImGui.Checkbox("Enable HealthBar", ref enableHealthInfo);
            ImGui.Checkbox("Disable Friendly Entity", ref enableFr);

            // team color
            if (ImGui.CollapsingHeader("Team Color"))
                ImGui.ColorPicker4("##teamcolor", ref teamColor);
            // enemy color
            if (ImGui.CollapsingHeader("Enemy Color"))
                ImGui.ColorPicker4("##enemycolor", ref enemyColor);
            // bone color
            if (ImGui.CollapsingHeader("Bone Color"))
                ImGui.ColorPicker4("##bonecolor", ref boneColor);

            // draw overlay
            DrawOverlay(screenSize);
            drawList = ImGui.GetWindowDrawList();


            // текст сверху
            drawList = ImGui.GetForegroundDrawList();
            string versionText = "eclipse_v1.2 by devlor";
            Vector2 textSize = ImGui.CalcTextSize(versionText);
            Vector2 pos = new Vector2(ImGui.GetIO().DisplaySize.X - textSize.X - 10, 10);

            // черная обводка
            var outlineColor = new System.Numerics.Vector4(0, 0, 0, 1);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    if (x != 0 || y != 0)
                        drawList.AddText(pos + new Vector2(x, y), ImGui.ColorConvertFloat4ToU32(outlineColor), versionText);

            // белый текст
            var mainColor = new System.Numerics.Vector4(1, 1, 1, 1);
            drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(mainColor), versionText);

            // draw stuff
            if (enableESP)
            {
                foreach (var entity in entities)
                {
                    // check if entity on screen
                    if (EntityOnScreen(entity))
                    {
                        // draw methods
                        DrawBox(entity);

                        if (enableLine)
                        {
                            DrawLine(entity);
                        }
                        if (enableBones)
                        {
                            DrawBones(entity);
                        }
                        if (enableHealthInfo)
                        {
                            DrawHealth(entity);
                        }
                    }
                }

            }
        }

        // check position
        bool EntityOnScreen(Entity entity)
        {
            if (entity.position2D.X > 0 && entity.position2D.X < screenSize.X && entity.position2D.Y > 0 && entity.position2D.Y < screenSize.Y)
            {
                return true;
            }
            return false;
        }

        // draw methods
        private void DrawHealth(Entity entity)
        {
            if (enableFr) // оффаем своих
            {
                if (entity.team == localPlayer.team)
                    return;
            }
            // calculate bar height

            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y + 5;

            // get box location
            float boxLeft = entity.viewPosition2D.X - entityHeight / 3;
            float boxRight = entity.viewPosition2D.X + entityHeight / 3;

            // calculate health bar wight
            float barPercentWight = 0.05f; // 5% of box wight
            float barPixelWight = barPercentWight * (boxRight - boxLeft);
            float barHeight = entityHeight * (entity.health / 100f);
            
            // calculate bar rectangle, two vectors
            Vector2 barTop = new Vector2 (boxLeft - barPixelWight, entity.position2D.Y - barHeight);
            Vector2 barBottom = new Vector2(boxLeft, entity.position2D.Y);

            Vector4 barColor;
            // get bar color
            if (entity.health > 75)
            {
                barColor = new Vector4(0, 1, 0, 1);
            }
            else
            {
                barColor = new Vector4(1f, 0.5f, 0f, 1f);
                if (entity.health < 40)
                {
                    barColor = new Vector4(1, 0, 0, 1);
                }
            }
            // draw health bar
            drawList.AddRectFilled(barTop, barBottom, ImGui.ColorConvertFloat4ToU32(barColor));
        } // health bar
        private void DrawBones(Entity entity)
        {
            if (enableFr) // оффаем своих
            {
                if (entity.team == localPlayer.team)
                    return;
            }

            uint uintColor = ImGui.ColorConvertFloat4ToU32(boneColor);

            float currentBoneThrisness = boneThicksness / entity.distance;

            drawList.AddLine(entity.bones2D[1], entity.bones2D[2], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[1], entity.bones2D[3], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[1], entity.bones2D[6], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[3], entity.bones2D[4], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[6], entity.bones2D[7], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[4], entity.bones2D[5], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[7], entity.bones2D[8], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[1], entity.bones2D[0], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[0], entity.bones2D[9], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[0], entity.bones2D[11], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[9], entity.bones2D[10], uintColor, currentBoneThrisness);
            drawList.AddLine(entity.bones2D[11], entity.bones2D[12], uintColor, currentBoneThrisness);
            drawList.AddCircle(entity.bones2D[2], 3 + currentBoneThrisness, uintColor);

        }// bones
        private void DrawBox(Entity entity) // box
        {
            // calculate box height
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;

            // calculate box dimensions
            Vector2 rectTop = new Vector2(entity.viewPosition2D.X - entityHeight / 3, entity.viewPosition2D.Y - 9); // высота 9

            Vector2 rectBottom = new Vector2(entity.position2D.X + entityHeight / 3, entity.position2D.Y);

            // disable friendly entities
            if (enableFr)
            {
                if (localPlayer.team != entity.team)
                {
                    Vector4 boxColor = enemyColor;
                    drawList.AddRect(rectTop, rectBottom, ImGui.ColorConvertFloat4ToU32(boxColor));
                }
            }
            else
            {
                Vector4 boxColor = localPlayer.team == entity.team ? teamColor : enemyColor;
                drawList.AddRect(rectTop, rectBottom, ImGui.ColorConvertFloat4ToU32(boxColor));
            }
        }

        private void DrawLine(Entity entity) // line
        {
            // get correct color
            Vector4 lineColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            // draw line
            drawList.AddLine(new Vector2(screenSize.X / 2, screenSize.Y), entity.position2D, ImGui.ColorConvertFloat4ToU32(lineColor));
        }


        //transfer any methods

        public void UpdateEntities(IEnumerable<Entity> newEntities)
        {
            entities = new ConcurrentQueue<Entity>(newEntities);
        } // update entities

        public void UpdateLocalPlayer(Entity newEntity) // update actor character
        {
            lock (entityLock) {
                localPlayer = newEntity;
            }
        }

        public Entity GetLocalPlayer() // get character ur actor 
        {
            lock(entityLock)
            {
                return localPlayer;
            }
        }
        void DrawOverlay(Vector2 screenSize) // overlay window  
        {
            ImGui.SetNextWindowSize(screenSize);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("overlay", ImGuiWindowFlags.NoDecoration |
                  ImGuiWindowFlags.NoBackground |
                  ImGuiWindowFlags.NoBringToFrontOnFocus |
                  ImGuiWindowFlags.NoMove |
                  ImGuiWindowFlags.NoInputs |
                  ImGuiWindowFlags.NoCollapse |
                  ImGuiWindowFlags.NoScrollbar |
                  ImGuiWindowFlags.NoScrollWithMouse
            );
        }

    }
}
