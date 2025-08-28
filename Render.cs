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
using Swed64;

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
        private bool enableWallHack = true;
        private bool enableLine = false;
        private bool enableBones = true;
        private bool enableFr = true;
        private bool enableHealthInfo = true;
        private bool enableNickName = true;
        private Vector4 enemyColor = new Vector4(1, 0, 0, 1); // red 
        private Vector4 teamColor = new Vector4(0, 1, 0, 1); // green
        private Vector4 boneColor = new Vector4(1, 1, 1, 1); // rgba white

        float boneThicksness = 4;

        // draw list
        ImDrawListPtr drawList;

        protected override void Render()
        {
            ImGui.Begin("eclispe");
            // ImGui Menu
            if (ImGui.BeginTabBar("eclipse tab"))
            {
                if (ImGui.BeginTabItem("Wallhack"))
                {
                    ImGui.Checkbox("Enable WallHack", ref enableWallHack);
                    ImGui.Checkbox("Enable ESP", ref enableESP);
                    ImGui.Checkbox("Enable Line", ref enableLine);
                    ImGui.Checkbox("Enable Bones", ref enableBones);
                    ImGui.Checkbox("Enable HealthBar", ref enableHealthInfo);
                    ImGui.Checkbox("Enable NickName", ref enableNickName);
                    ImGui.Checkbox("Disable Friendly Entity", ref enableFr);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Color"))
                {
                    // team color
                    if (ImGui.CollapsingHeader("Team Color"))
                        ImGui.ColorPicker4("##teamcolor", ref teamColor);
                    // enemy color
                    if (ImGui.CollapsingHeader("Enemy Color"))
                        ImGui.ColorPicker4("##enemycolor", ref enemyColor);
                    // bone color
                    if (ImGui.CollapsingHeader("Bone Color"))
                        ImGui.ColorPicker4("##bonecolor", ref boneColor);
                }
                ImGui.EndTabBar();
            }

            // draw overlay
            DrawOverlay(screenSize);
            drawList = ImGui.GetWindowDrawList();


            // текст сверху
            drawList = ImGui.GetForegroundDrawList();
            string versionText = "eclipse_v1.3.1 by devlor";
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
            if (enableWallHack)
            {
                foreach (var entity in entities)
                {
                    // check if entity on screen
                    if (EntityOnScreen(entity))
                    {
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
                        if (enableESP)
                        {
                            DrawBox(entity);
                        }
                        if (enableNickName)
                        {
                            DrawName(entity);
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

        private void DrawName(Entity entity)
        {
            if (enableFr) // оффаем своих
            {
                if (entity.team == localPlayer.team)
                    return;
            }

            // get box location
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;
            float boxLeft = entity.viewPosition2D.X - entityHeight / 3;
            float boxRight = entity.viewPosition2D.X + entityHeight / 3;

            // позиция текста под боксом
            float nameX = (boxLeft + boxRight) / 2 - 25;
            float nameY = entity.position2D.Y;
            // ======== рисуем ========
            Vector4 nameColor = new Vector4(1f, 1f, 1f, 1f); // белый цвет
            drawList.AddText(new Vector2(nameX, nameY),
                ImGui.ColorConvertFloat4ToU32(nameColor),
                entity.name);
        }

        private void DrawHealth(Entity entity)
        {
            if (enableFr && entity.team == localPlayer.team)
                return;

            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y + 5;

            float boxLeft = entity.viewPosition2D.X - entityHeight / 3;
            float boxRight = entity.viewPosition2D.X + entityHeight / 3;

            float barWidthPercent = 0.05f;
            float barWidth = Math.Clamp(barWidthPercent * (boxRight - boxLeft), 2f, 6f);

            float barHeight = entityHeight;
            float healthPercent = Math.Clamp(entity.health / 100f, 0f, 1f);

            // фон (пустой хп)
            Vector2 barBgTop = new Vector2(boxLeft - barWidth, entity.viewPosition2D.Y);
            Vector2 barBgBottom = new Vector2(boxLeft, entity.position2D.Y);
            drawList.AddRectFilled(barBgTop, barBgBottom, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)));

            // текущий хп
            Vector2 barTop = new Vector2(boxLeft - barWidth, entity.position2D.Y - barHeight * healthPercent);
            Vector2 barBottom = new Vector2(boxLeft, entity.position2D.Y);

            // плавный цвет от красного до зелёного
            Vector4 barColor = new Vector4(1f - healthPercent, healthPercent, 0f, 1f);

            drawList.AddRectFilled(barTop, barBottom, ImGui.ColorConvertFloat4ToU32(barColor));
        }
        private void DrawBones(Entity entity)
        {
            if (enableFr && entity.team == localPlayer.team)
                return;

            uint uintColor = ImGui.ColorConvertFloat4ToU32(boneColor);

            // ограничиваем толщину (не меньше 0.5 и не больше 2.5)
            float currentBoneThickness = Math.Clamp(boneThicksness / entity.distance, 1.0f, 2.5f);

            // пары костей (родитель -> ребёнок)
            int[,] bonePairs =
            {
              {1, 2}, {1, 3}, {1, 6},
              {3, 4}, {4, 5}, {6, 7}, {7, 8},
              {1, 0}, {0, 9}, {0, 11},
              {9, 10}, {11, 12}
    };

            // рисуем линии
            for (int i = 0; i < bonePairs.GetLength(0); i++)
            {
                int a = bonePairs[i, 0];
                int b = bonePairs[i, 1];

                drawList.AddLine(entity.bones2D[a], entity.bones2D[b], uintColor, currentBoneThickness);
            }

            // голова (обычно кость 2)
            drawList.AddCircle(entity.bones2D[2], 3 + currentBoneThickness, uintColor);
        }
        private void DrawBox(Entity entity)
        {
            if (enableFr && entity.team == localPlayer.team)
                return; // не рисуем своих

            // высота бокса
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;
            float widthFactor = 0.33f; // половина ширины бокса относительно высоты

            // центр по X
            float centerX = (entity.viewPosition2D.X + entity.position2D.X) / 2;

            // координаты бокса
            Vector2 rectTop = new Vector2(centerX - entityHeight * widthFactor, entity.viewPosition2D.Y - 5); // сверху немного выше головы
            Vector2 rectBottom = new Vector2(centerX + entityHeight * widthFactor, entity.position2D.Y); // до ног

            // цвет
            Vector4 boxColor = (entity.team == localPlayer.team) ? teamColor : enemyColor;

            drawList.AddRect(rectTop, rectBottom, ImGui.ColorConvertFloat4ToU32(boxColor));
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
