using System.IO;
using Ironhorse.Core;
using Ironhorse.Game;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>The save on disk survives failed writes, crashes mid-save and damaged files.
    /// Runs in Unity only (SaveSystem uses JsonUtility); Tools/CoreTests skips this folder.</summary>
    public class SaveSystemTests
    {
        string _dir, _main;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ironhorse-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SaveSystem.Folder = _dir;
            _main = SaveSystem.PathFor("ironhorse_save.json");
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Folder = null;
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        static GameState Railroad(int coins)
        {
            var s = GameSession.NewGame(1_000_000, 7);
            s.coins = coins;
            return s;
        }

        [Test]
        public void SaveThenLoad_RoundTrips_AndKeepsThePreviousSaveAsBackup()
        {
            Assert.IsTrue(SaveSystem.Save(Railroad(100)));
            Assert.IsTrue(SaveSystem.Save(Railroad(200)));
            Assert.AreEqual(200, SaveSystem.Load().coins);
            Assert.IsTrue(File.Exists(_main + ".bak"));
            Assert.IsFalse(File.Exists(_main + ".tmp"), "no temp file left after a good save");
            File.Delete(_main);
            Assert.AreEqual(100, SaveSystem.Load().coins, "the backup is the save before");
        }

        [Test]
        public void FailedWrite_ReturnsFalse_AndLeavesTheLastGoodSave()
        {
            Assert.IsTrue(SaveSystem.Save(Railroad(100)));
            Directory.CreateDirectory(_main + ".tmp"); // the temp file can't be written (stands in for a full disk)
            Assert.IsFalse(SaveSystem.Save(Railroad(999)));
            Directory.Delete(_main + ".tmp");
            Assert.AreEqual(100, SaveSystem.Load().coins);
            Assert.IsTrue(SaveSystem.Save(Railroad(300)), "saving works again once there's room");
            Assert.AreEqual(300, SaveSystem.Load().coins);
        }

        [Test]
        public void CorruptedMainSave_FallsBackToTheBackup()
        {
            Assert.IsTrue(SaveSystem.Save(Railroad(100)));
            Assert.IsTrue(SaveSystem.Save(Railroad(200)));
            File.WriteAllText(_main, "{\"coins\":200,\"trains\":[{\"uid\":1,\"defId\":\"du"); // cut off mid-write
            Assert.AreEqual(100, SaveSystem.Load().coins);
            File.WriteAllText(_main, "");
            Assert.AreEqual(100, SaveSystem.Load().coins, "an empty file too");
            File.WriteAllText(_main, "{\"coins\":5}");
            Assert.AreEqual(100, SaveSystem.Load().coins, "a save with no engines isn't a railroad");
        }

        [Test]
        public void CrashBetweenWritingAndMoving_LoadsTheCheckedTempFile()
        {
            Assert.IsTrue(SaveSystem.Save(Railroad(100)));
            Assert.IsTrue(SaveSystem.Save(Railroad(200)));
            // The newest save was written and checked, then the app died after removing the old main file.
            File.WriteAllText(_main + ".tmp", UnityEngine.JsonUtility.ToJson(Railroad(300)));
            File.Delete(_main);
            Assert.AreEqual(300, SaveSystem.Load().coins);
        }

        [Test]
        public void CrashMidWrite_HalfTempFileIsIgnored_AndTheNextSaveSucceeds()
        {
            Assert.IsTrue(SaveSystem.Save(Railroad(100)));
            File.WriteAllText(_main + ".tmp", "{\"coins\":4"); // died while writing
            Assert.AreEqual(100, SaveSystem.Load().coins);
            Assert.IsTrue(SaveSystem.Save(Railroad(400)));
            Assert.AreEqual(400, SaveSystem.Load().coins);
        }

        [Test]
        public void NothingOnDisk_LoadsNull_AndDeleteClearsEverything()
        {
            Assert.IsNull(SaveSystem.Load());
            Assert.IsTrue(SaveSystem.Save(Railroad(1)));
            Assert.IsTrue(SaveSystem.Save(Railroad(2)));
            File.WriteAllText(_main + ".tmp", "x");
            SaveSystem.Delete();
            Assert.IsNull(SaveSystem.Load());
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length);
        }
    }
}
