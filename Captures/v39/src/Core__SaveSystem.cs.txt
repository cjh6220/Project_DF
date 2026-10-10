using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Proto.Core
{
    /// <summary>세이브 파일 한 장의 내용. JsonUtility가 사전(Dictionary)을 못 써서 이름 · 값 목록 두 개로 둔다.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;

        // 창고 자원
        public List<string> resIds = new();
        public List<int> resAmounts = new();

        // 성장 (왼쪽 장로)
        public List<string> nodeIds = new();
        public List<int> nodeLevels = new();

        // 스킬 (오른쪽 검객)
        public int sp;
        public List<string> skillNames = new();
        public List<int> skillLevels = new();
        public List<string> slots = new();

        // 던전
        public List<string> cleared = new();
        public List<string> seen = new();
        public string target;
        public int bestRooms;
    }

    /// <summary>
    /// 세이브 · 불러오기 · 초기화.
    ///
    ///   저장 위치   Application.persistentDataPath/save.json  (임시 파일에 쓰고 바꿔 끼운다 — 쓰다 꺼져도 깨지지 않게)
    ///   저장 시점   마을 도착 · 정산 · 성장/스킬 구매 · 행선지 변경 · 게임 종료 (SaveAgent가 모아서 0.5초 뒤 한 번)
    ///   던전 중 종료  이번 판에 얻은 자원도 창고에 더해서 저장한다 — 포기와 같은 취급, 잃는 게 없다
    ///   초기화      파일을 지우고 씬을 처음부터 다시 연다
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        public static bool HasSave => File.Exists(FilePath);

        /// <summary>초기화 중에는 저장하지 않는다 — 씬이 내려가며 종료 저장이 지운 파일을 되살리면 안 된다.</summary>
        public static bool Suspended { get; private set; }

        /// <summary>게임 시작 때 RunManager가 부른다. 세이브가 있으면 넣고, 자동 저장 담당을 붙인다.</summary>
        public static void LoadInto(RunManager run, Proto.Town.VillageController village)
        {
            Suspended = false;
            var data = Read();
            if (data != null) Apply(data, run, village);

            var agent = run.GetComponent<SaveAgent>();
            if (agent == null) agent = run.gameObject.AddComponent<SaveAgent>();
            agent.Bind(run, village);
        }

        static SaveData Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data != null) Debug.Log($"[Save] 불러옴 ({data.savedAt}) — {FilePath}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] 세이브를 읽지 못해 새로 시작합니다: " + e.Message);
                return null;
            }
        }

        static void Apply(SaveData d, RunManager run, Proto.Town.VillageController village)
        {
            run.Bank.Clear();
            for (int i = 0; i < d.resIds.Count && i < d.resAmounts.Count; i++)
                if (Enum.TryParse(d.resIds[i], out ResourceId id)) run.Bank.Add(id, d.resAmounts[i]);

            run.Progression.Import(d.nodeIds, d.nodeLevels);
            run.Skills.Import(d.skillNames, d.skillLevels, d.slots, d.sp);
            run.ImportSave(d.cleared, d.bestRooms);
            if (village != null) village.ImportSave(d.target, d.seen);
        }

        public static void Save(RunManager run, Proto.Town.VillageController village)
        {
            if (Suspended || run == null) return;
            var d = new SaveData { savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            // 던전 안에서 저장하면(게임 종료) 이번 판 획득물도 창고에 더한 값으로 쓴다
            var sum = new Dictionary<ResourceId, int>();
            foreach (var kv in run.Bank.All) sum[kv.Key] = kv.Value;
            if (run.Phase == RunPhase.InDungeon)
                foreach (var kv in run.RunLoot.All) sum[kv.Key] = (sum.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            foreach (var kv in sum) { d.resIds.Add(kv.Key.ToString()); d.resAmounts.Add(kv.Value); }

            run.Progression.Export(d.nodeIds, d.nodeLevels);
            run.Skills.Export(d.skillNames, d.skillLevels, d.slots, out d.sp);
            d.cleared.AddRange(run.ClearedIds);
            d.bestRooms = run.BestRooms;
            if (village != null) { d.seen.AddRange(village.SeenIds); d.target = village.TargetId; }

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + ".bak");
                else File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] 저장 실패: " + e.Message);
            }
        }

        /// <summary>저장 초기화 — 파일을 지우고 처음부터 다시 시작한다.</summary>
        public static void ResetAndRestart()
        {
            Suspended = true;
            try
            {
                foreach (var p in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
                    if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception e) { Debug.LogWarning("[Save] 세이브 삭제 실패: " + e.Message); }
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
