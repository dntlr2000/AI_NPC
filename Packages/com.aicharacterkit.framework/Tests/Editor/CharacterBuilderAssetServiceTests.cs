using AiCharacterKit.Core;
using AiCharacterKit.Editor;
using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies consumer-owned profile authoring and deterministic editor preview behavior.
    /// </summary>
    public sealed class CharacterBuilderAssetServiceTests
    {
        private const string TestFolder = "Assets/__AICharacterKitPhase10Tests";

        /// <summary>
        /// Creates a clean writable Assets folder for one isolated editor test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DeleteTestFolder();
            AssetDatabase.CreateFolder("Assets", "__AICharacterKitPhase10Tests");
        }

        /// <summary>
        /// Removes every temporary consumer asset created by the current test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            DeleteTestFolder();
        }

        /// <summary>
        /// Confirms profile creation preserves every draft field at one unique Assets path.
        /// </summary>
        [Test]
        public void CreateCharacterProfile_ValidDraft_PreservesValuesAndUsesUniquePath()
        {
            var firstDraft = CreateProfileDraft("Guide/Profile", "guide-one");
            var secondDraft = CreateProfileDraft("Guide/Profile", "guide-two");
            var reservedDraft = CreateProfileDraft("CON", "guide-three");

            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    firstDraft,
                    TestFolder,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    secondDraft,
                    TestFolder,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    reservedDraft,
                    TestFolder,
                    out var reserved,
                    out var reservedError),
                Is.True,
                reservedError);

            Assert.That(first.CharacterId, Is.EqualTo("guide-one"));
            Assert.That(first.DisplayName, Is.EqualTo("Guide"));
            Assert.That(first.DefaultEmotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(AssetDatabase.GetAssetPath(first), Does.StartWith(TestFolder + "/"));
            Assert.That(AssetDatabase.GetAssetPath(first), Does.Not.Contain("/Profile/"));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.Not.EqualTo(
                AssetDatabase.GetAssetPath(first)));
            Assert.That(
                AssetDatabase.GetAssetPath(reserved),
                Does.EndWith("/CON_.asset"));
        }

        /// <summary>
        /// Confirms invalid values and folders fail before creating any profile asset.
        /// </summary>
        [Test]
        public void CreateCharacterProfile_InvalidInput_DoesNotCreateAsset()
        {
            var draft = CreateProfileDraft("Invalid", "invalid-guide");
            draft.Personality = "   ";

            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    draft,
                    TestFolder,
                    out var invalidProfile,
                    out var validationError),
                Is.False);
            Assert.That(invalidProfile, Is.Null);
            Assert.That(validationError, Is.Not.Empty);

            draft.Personality = "Helpful.";
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    draft,
                    "Packages/com.aicharacterkit.framework",
                    out var packageProfile,
                    out var pathError),
                Is.False);
            Assert.That(packageProfile, Is.Null);
            Assert.That(pathError, Does.Contain("Assets"));
            Assert.That(AssetDatabase.FindAssets(
                "t:CharacterProfile",
                new[] { TestFolder }), Is.Empty);
        }

        /// <summary>
        /// Confirms an existing profile is updated only after detached values pass runtime validation.
        /// </summary>
        [Test]
        public void UpdateCharacterProfile_InvalidThenValid_PreservesThenUpdatesAsset()
        {
            var draft = CreateProfileDraft("Guide", "guide-update");
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    draft,
                    TestFolder,
                    out var profile,
                    out var createError),
                Is.True,
                createError);

            var update = CharacterProfileDraft.FromProfile(profile);
            update.DisplayName = "   ";
            Assert.That(
                CharacterBuilderAssetService.TryUpdateCharacterProfile(
                    profile,
                    update,
                    out _),
                Is.False);
            Assert.That(profile.DisplayName, Is.EqualTo("Guide"));

            update.DisplayName = "Updated Guide";
            update.ExampleDialogue = "업데이트된 안내입니다.";
            Assert.That(
                CharacterBuilderAssetService.TryUpdateCharacterProfile(
                    profile,
                    update,
                    out var updateError),
                Is.True,
                updateError);
            Assert.That(profile.DisplayName, Is.EqualTo("Updated Guide"));
            Assert.That(profile.ExampleDialogue, Is.EqualTo("업데이트된 안내입니다."));
        }

        /// <summary>
        /// Confirms duplicate character IDs remain visible warnings rather than creation blockers.
        /// </summary>
        [Test]
        public void DuplicateCharacterIdProfiles_AreReportedWithoutBlockingCreation()
        {
            var firstDraft = CreateProfileDraft("First", "shared-guide");
            var secondDraft = CreateProfileDraft("Second", "shared-guide");
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    firstDraft,
                    TestFolder,
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                CharacterBuilderAssetService.TryCreateCharacterProfile(
                    secondDraft,
                    TestFolder,
                    out var second,
                    out var secondError),
                Is.True,
                secondError);

            var duplicates =
                CharacterBuilderAssetService.FindDuplicateCharacterIdPaths(
                    first.CharacterId,
                    first);
            Assert.That(duplicates, Has.Count.EqualTo(1));
            Assert.That(duplicates[0], Is.EqualTo(AssetDatabase.GetAssetPath(second)));
        }

        /// <summary>
        /// Confirms the editor preview uses the deterministic zero-latency Mock path.
        /// </summary>
        [Test]
        public void PreviewMock_GreetingAndInvalidInput_ReturnStableSafeOutcomes()
        {
            var draft = CreateProfileDraft("Preview", "preview-guide");

            Assert.That(
                CharacterBuilderAssetService.TryPreviewMock(
                    draft,
                    "hello",
                    out var first,
                    out var firstError),
                Is.True,
                firstError);
            Assert.That(
                CharacterBuilderAssetService.TryPreviewMock(
                    draft,
                    "hello",
                    out var second,
                    out var secondError),
                Is.True,
                secondError);
            Assert.That(second.Dialogue, Is.EqualTo(first.Dialogue));
            Assert.That(first.Emotion, Is.EqualTo(NpcEmotion.Happy));
            Assert.That(first.Gesture, Is.EqualTo(NpcGesture.Wave));

            Assert.That(
                CharacterBuilderAssetService.TryPreviewMock(
                    draft,
                    "   ",
                    out var invalid,
                    out var invalidError),
                Is.False);
            Assert.That(invalid, Is.Null);
            Assert.That(invalidError, Is.Not.Empty);
        }

        /// <summary>
        /// Confirms opaque voice profile creation and updates use existing contract validation.
        /// </summary>
        [Test]
        public void VoiceProfile_CreateAndUpdate_UsesOpaquePresetValidation()
        {
            var draft = new VoiceProfileDraft
            {
                AssetName = "Guide Voice",
                VoicePresetId = "guide-warm-01"
            };
            Assert.That(
                CharacterBuilderAssetService.TryCreateVoiceProfile(
                    draft,
                    TestFolder,
                    out var profile,
                    out var createError),
                Is.True,
                createError);
            Assert.That(profile.VoicePresetId, Is.EqualTo("guide-warm-01"));

            var update = VoiceProfileDraft.FromProfile(profile);
            update.VoicePresetId = "Invalid Voice Name";
            Assert.That(
                CharacterBuilderAssetService.TryUpdateVoiceProfile(
                    profile,
                    update,
                    out _),
                Is.False);
            Assert.That(profile.VoicePresetId, Is.EqualTo("guide-warm-01"));

            update.VoicePresetId = null;
            Assert.That(
                CharacterBuilderAssetService.TryUpdateVoiceProfile(
                    profile,
                    update,
                    out var nullError),
                Is.False);
            Assert.That(nullError, Is.Not.Empty);
            Assert.That(profile.VoicePresetId, Is.EqualTo("guide-warm-01"));

            update.VoicePresetId = "guide-calm-02";
            Assert.That(
                CharacterBuilderAssetService.TryUpdateVoiceProfile(
                    profile,
                    update,
                    out var updateError),
                Is.True,
                updateError);
            Assert.That(profile.VoicePresetId, Is.EqualTo("guide-calm-02"));
        }

        /// <summary>
        /// Creates one complete reusable draft shared by profile asset test cases.
        /// </summary>
        private static CharacterProfileDraft CreateProfileDraft(
            string assetName,
            string characterId)
        {
            return new CharacterProfileDraft
            {
                AssetName = assetName,
                CharacterId = characterId,
                DisplayName = "Guide",
                Personality = "Helpful and observant.",
                SpeechStyle = "Warm, concise sentences.",
                ExampleDialogue = "안내해 드리겠습니다.",
                DefaultEmotion = NpcEmotion.Happy
            };
        }

        /// <summary>
        /// Deletes only the fixed test-owned Assets folder when it exists.
        /// </summary>
        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
