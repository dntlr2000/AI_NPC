using AiCharacterKit.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Verifies the minimum data required for reusable character profiles.
    /// </summary>
    public sealed class CharacterProfileTests
    {
        /// <summary>
        /// Confirms that the default complete profile satisfies runtime validation.
        /// </summary>
        [Test]
        public void TryValidate_CompleteProfile_ReturnsTrue()
        {
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();

            try
            {
                Assert.That(profile.TryValidate(out var error), Is.True);
                Assert.That(error, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        /// <summary>
        /// Confirms that whitespace in any required text field is rejected.
        /// </summary>
        [TestCase("characterId")]
        [TestCase("displayName")]
        [TestCase("personality")]
        [TestCase("speechStyle")]
        [TestCase("exampleDialogue")]
        public void TryValidate_RequiredTextIsWhitespace_ReturnsFalse(
            string propertyName)
        {
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();

            try
            {
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty(propertyName).stringValue = "   ";
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out var error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        /// <summary>
        /// Confirms that a serialized emotion outside the supported enum is rejected.
        /// </summary>
        [Test]
        public void TryValidate_UnsupportedEmotion_ReturnsFalse()
        {
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();

            try
            {
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.FindProperty("defaultEmotion").intValue = 999;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.TryValidate(out var error), Is.False);
                Assert.That(error, Does.Contain("emotion").IgnoreCase);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
