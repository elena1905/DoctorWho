using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework;

namespace DoctorWho
{
    public class Sound : Entity
    {
        public const float Radius = 0.2f;

        public SoundEffect SoundEffect;
        public SoundEffectInstance SoundEffectInstance;
        public string SoundName;

        public Sound()
        {
            SoundName = @"Sounds\Theme";
        }

        public override void LoadContent()
        {
            SoundEffect = XNAGame.Instance().Content.Load<SoundEffect>(SoundName);

            // Instantiate SoundEffectInstance to gain more control access upon sound
            SoundEffectInstance = SoundEffect.CreateInstance();
            SoundEffectInstance.Volume = 0.1f;      // Set volume
            SoundEffectInstance.IsLooped = true;    // Set the sound looping
            SoundEffectInstance.Play();             // Play the sound
        }

        public override void UnloadContent()
        {
            
        }

        public override void Draw(GameTime gameTime)
        {
            
        }

        public override void Update(GameTime gameTime)
        {
            
        }
    }
}
