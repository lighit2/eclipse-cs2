using eclipse_external;
using Swed64;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

// main logic

// init swed
Swed swed = new Swed("cs2");

// get client module
IntPtr client = swed.GetModuleBase("client.dll");

// init render
Renderer renderer = new Renderer();

// safe renderer start
Thread renderThread = new Thread(() =>
{
    try
    {
        var mi = renderer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => string.Equals(m.Name, "Start", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Name, "StartAsync", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(m.Name, "Run", StringComparison.OrdinalIgnoreCase));
        if (mi != null)
        {
            var res = mi.Invoke(renderer, null);
            if (res is Task t) t.GetAwaiter().GetResult();
        }
        else
        {
            var m2 = renderer.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.Public);
            m2?.Invoke(renderer, null);
        }
    }
    catch
    {
        // don;t crush
    }
})
{ IsBackground = true };
try { renderThread.SetApartmentState(ApartmentState.STA); } catch { }
renderThread.Start();

// get screen size from renderer
Vector2 screenSize;
try { screenSize = renderer.screenSize == default ? new Vector2(1920, 1080) : renderer.screenSize; }
catch { screenSize = new Vector2(1920, 1080); }

// store entities
List<Entity> entities = new List<Entity>();
Entity localPlayer = new Entity();

// offsets.cs
int dwEntityList = 0x1D154C8;
int dwViewMatrix = 0x1E32830;
int dwLocalPlayerPawn = 0x1BF14A0;

// client.dll
int m_vOldOrigin = 0x15B0;
int m_iTeamNum = 0x3EB;
int m_lifeState = 0x350;
int m_hPlayerPawn = 0x8FC;
int m_vecViewOffset = 0xD98;
int m_modelState = 0x190;
int m_pGameSceneNode = 0x330;
int m_iHealth = 0x34C;


// anti-detect
Random rnd = new Random(unchecked(Environment.TickCount * 31 + DateTime.Now.Millisecond));

IntPtr clientBase = IntPtr.Zero;
int clientUpdateTime = 0;

IntPtr localPlayerPawnCached = IntPtr.Zero;
int localPlayerUpdateTime = 0;

// кеш для lerp
Dictionary<IntPtr, Vector2> prevPositions = new Dictionary<IntPtr, Vector2>();
Dictionary<IntPtr, Vector2> prevViewPositions = new Dictionary<IntPtr, Vector2>();
static bool IsFinite(Vector2 v) => !(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y));


// ESP Loop
while (true)
{
    int currentTime = Environment.TickCount;

    try
    {
        // -------------------- client.dll !!
        if (currentTime - clientUpdateTime > rnd.Next(500, 1001))
        {
            clientBase = swed.GetModuleBase("client.dll");
            clientUpdateTime = currentTime;
        }
        if (clientBase == IntPtr.Zero) { Thread.Sleep(200); continue; }

        entities.Clear();

        // -------------------- Player Pawn !!
        if (currentTime - localPlayerUpdateTime > rnd.Next(500, 801))
        {
            localPlayerPawnCached = swed.ReadPointer(clientBase, dwLocalPlayerPawn);
            localPlayerUpdateTime = currentTime;

            if (localPlayerPawnCached != IntPtr.Zero)
            {
                try { localPlayer.team = swed.ReadInt(localPlayerPawnCached, m_iTeamNum); } catch { }
            }
        }

        // get entity list
        IntPtr entityList = swed.ReadPointer(clientBase, dwEntityList);
        if (entityList == IntPtr.Zero) { Thread.Sleep(30); continue; }

        // make entry
        IntPtr listEntry = swed.ReadPointer(entityList, 0x10);
        if (listEntry == IntPtr.Zero) { Thread.Sleep(20); continue; }

        // get local player
        IntPtr localPlayerPawn = swed.ReadPointer(clientBase, dwLocalPlayerPawn);
        if (localPlayerPawn != IntPtr.Zero)
        {
            try { localPlayer.team = swed.ReadInt(localPlayerPawn, m_iTeamNum); } catch { }
        }

        // get matrix ONCE per frame
        float[] viewMatrix = swed.ReadMatrix(clientBase + dwViewMatrix);
        if (viewMatrix == null) { Thread.Sleep(20); continue; }

        // -------------------- random entity !!!
        int[] indices = Enumerable.Range(0, 64).ToArray();
        for (int k = 63; k > 0; k--)
        {
            int j = rnd.Next(0, k + 1);
            int tmp = indices[k];
            indices[k] = indices[j];
            indices[j] = tmp;
        }

        // populate entities using shuffled indices
        for (int ii = 0; ii < indices.Length; ii++)
        {
            int i = indices[ii];

            // get current controller
            IntPtr currentController = swed.ReadPointer(listEntry, i * 0x78);
            if (currentController == IntPtr.Zero) continue;

            int pawnHandle = swed.ReadInt(currentController, m_hPlayerPawn);
            if (pawnHandle == 0) continue;

            // get current pawn
            IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
            if (listEntry2 == IntPtr.Zero) continue;

            IntPtr currentPawn = swed.ReadPointer(listEntry2, 0x78 * (pawnHandle & 0x1FF));
            if (currentPawn == IntPtr.Zero) continue;

            // bone
            IntPtr sceneNode = swed.ReadPointer(currentPawn, m_pGameSceneNode);
            IntPtr boneMatrix = swed.ReadPointer(sceneNode, m_modelState + 0x80); // would be dwBoneMatrix

            // check if lifestate
            int lifeState = swed.ReadInt(currentPawn, m_lifeState);
            if (lifeState != 256) continue;

            // populate entity
            Entity entity = new Entity();
            try
            {
                entity.team = swed.ReadInt(currentPawn, m_iTeamNum);
                entity.position = swed.ReadVec(currentPawn, m_vOldOrigin);
                entity.viewOffset = swed.ReadVec(currentPawn, m_vecViewOffset);
                entity.distance = Vector3.Distance(entity.position, localPlayer.position);
                entity.bones = Calculate.ReadBones(boneMatrix, swed);
                entity.bones2D = Calculate.ReadBones2D(entity.bones, viewMatrix, screenSize);
                entity.health = swed.ReadInt(currentPawn, m_iHealth);
            }
            catch
            {
                continue;
            }

            // для ног и головы
            Vector2 targetPos = Calculate.WorldToScreen(viewMatrix, entity.position, screenSize);
            Vector2 targetViewPos = Calculate.WorldToScreen(viewMatrix, Vector3.Add(entity.position, entity.viewOffset), screenSize);

            // validate
            if (!IsFinite(targetPos) && !IsFinite(targetViewPos)) continue;

            // -------------------- lerp для плавности (используем кеш)
            if (prevPositions.TryGetValue(currentPawn, out Vector2 prevPos))
                entity.position2D = IsFinite(prevPos) ? Vector2.Lerp(prevPos, targetPos, 0.3f) : targetPos;
            else
                entity.position2D = targetPos;

            if (prevViewPositions.TryGetValue(currentPawn, out Vector2 prevView))
                entity.viewPosition2D = IsFinite(prevView) ? Vector2.Lerp(prevView, targetViewPos, 0.3f) : targetViewPos;
            else
                entity.viewPosition2D = targetViewPos;

            // update cache
            prevPositions[currentPawn] = entity.position2D;
            prevViewPositions[currentPawn] = entity.viewPosition2D;

            entities.Add(entity);
        }

        // -------------------- очистка кеша от устаревших объектов
        var currentPawns = new HashSet<IntPtr>(entities.Select(e => e.Pointer));
        foreach (var key in prevPositions.Keys.ToList())
        {
            if (!currentPawns.Contains(key))
                prevPositions.Remove(key);
        }
        foreach (var key in prevViewPositions.Keys.ToList())
        {
            if (!currentPawns.Contains(key))
                prevViewPositions.Remove(key);
        }

        // update renderer
        try
        {
            renderer.UpdateLocalPlayer(localPlayer);
            renderer.UpdateEntities(entities);
        }
        catch
        {
        // swallowing render exceptions
        }

        // единый sleep: base + jitter + редкий длинный пробел (UNDETECTED) бля буду
        int baseDelay = rnd.Next(8, 16);  // 8–15 ms
        int jitterMs = rnd.Next(0, 4);    // 0–3 ms
        //int longPause = (rnd.NextDouble() < 0.012) ? (50 + rnd.Next(0, 150)) : 0; // редкая длинная пауза для производительности CPU
        //Thread.Sleep(baseDelay + jitterMs + longPause);
    }
    catch
    {
        Thread.Sleep(200);
    }

}
