﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace DoctorWho
{
    class AIFighter:Fighter
    {
        State currentState;
        public AIFighter()
            : base()
        {
            ModelName = "Ship1";
        }

        public override void Update(GameTime gameTime)
        {
            if (currentState != null)
            {
                currentState.Update(gameTime);
            }
            base.Update(gameTime);
        }

        public void SwicthState(State newState)
        {
            if (currentState != null)
            {
                currentState.Exit();
            }
            
            currentState = newState;
            if (newState != null)
            {
                currentState.Enter();
            }
        }
    }
}
