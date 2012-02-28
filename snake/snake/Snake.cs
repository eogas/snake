using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using ProjectMercury;
using ProjectMercury.Renderers;
using System.Collections;

namespace snake
{
	// Data structures
	enum Direction { Up, Down, Left, Right };
	enum GameState { Splash, Playing, Dying, HiScore };

	public class HighScore : IComparable
	{
		public string Name { get; set; }
		public int Score { get; set; }

		public HighScore()
		{
			Name = "default";
			Score = 0;
		}

		public HighScore(string s, int i)
		{
			Name = s;
			Score = i;
		}

		public int CompareTo(object obj)
		{
			HighScore hs = obj as HighScore;

			if (hs == null) return 1;

			return Score.CompareTo(hs.Score);
		}

		public override string ToString()
		{
			return String.Format("{0} -> {1}", Name, Score);
		}
	}

	public class HighScoreList
	{
		private int numScores = 6;

		public HighScoreList()
		{
			Scores = new List<HighScore>();
		}

		public bool isHighScore(int score)
		{
			if (Scores.Count < numScores)
				return true;

			foreach (HighScore s in Scores)
			{
				if (score > s.Score)
				{
					return true;
				}
			}

			return false;
		}

		public List<HighScore> Scores;

		public void Add(HighScore score)
		{
			if (Scores.Count >= numScores)
			{
				Scores.Sort();
				Scores.RemoveAt(0);
			}
			Scores.Add(score);
			Scores.Sort();
			Scores.Reverse();
		}

		public IEnumerator GetEnumerator()
		{
			return Scores.GetEnumerator();
		}
	}

	// Main game class
	public class Snake : Microsoft.Xna.Framework.Game
	{
		int blockSize = 32;
		int levelWidth = 10;
		int levelHeight = 10;
		Point nextBlock;
		Point[] constrictBlocks;
		Random rand;
		int score = 0;
		GameState state;

		List<Point> snakeBlocks;
		Direction snakeDirection;
		Direction nextSnakeDirection;

		int stepTime = 200;
		int timeSinceLastStep = 0;
		List<Keys> keysDown;

		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;

		Renderer renderer;
		Texture2D blockTexture;
		ParticleEffect explode;

		SpriteFont sfScore;
		SpriteFont sfSplash;
		Vector2 splashVec, enterVec;

		ModalHiScoreDialog dlgHiScore;
		HighScoreList highScores;
		XmlSerializer xmler;

		SoundEffect crunch, boom, hurt;

		public Snake()
		{
			graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";

			dlgHiScore = new ModalHiScoreDialog();
			xmler = new XmlSerializer(typeof(HighScoreList));

			if (File.Exists(System.Environment.CurrentDirectory + "\\HighScores.xml"))
			{
				XmlReader reader = XmlReader.Create(System.Environment.CurrentDirectory + "\\HighScores.xml");
				highScores = xmler.Deserialize(reader) as HighScoreList;
				reader.Close();
				highScores.Scores.Sort();
				highScores.Scores.Reverse();
			}
			else
				highScores = new HighScoreList();

			renderer = new SpriteBatchRenderer()
			{
				GraphicsDeviceService = graphics
			};
			explode = new ParticleEffect();

			crunch = Content.Load<SoundEffect>("crunch");
			boom = Content.Load<SoundEffect>("boom");
			hurt = Content.Load<SoundEffect>("hurt");
		}

		protected override void Initialize()
		{
			graphics.PreferredBackBufferWidth = blockSize * levelWidth;
			graphics.PreferredBackBufferHeight = blockSize * levelHeight;
			graphics.ApplyChanges();

			keysDown = new List<Keys>();
			rand = new Random();

			state = GameState.Splash;
			InitSnake();

			base.Initialize();
		}

		private void InitSnake()
		{
			snakeBlocks = new List<Point>();
			snakeBlocks.Add(new Point((levelWidth / 2) + 1, (levelHeight / 2) - 2));
			snakeBlocks.Add(new Point((levelWidth / 2) + 1, (levelHeight / 2) - 1));
			snakeBlocks.Add(new Point((levelWidth / 2) + 1, (levelHeight / 2)));
			snakeBlocks.Add(new Point((levelWidth / 2) + 1, (levelHeight / 2) + 1));

			snakeDirection = Direction.Up;
			nextSnakeDirection = Direction.Up;

			stepTime = 200;

			GenerateBlock();
		}

		private void GenerateBlock()
		{
			do
			{
				nextBlock = new Point(rand.Next(levelWidth), rand.Next(levelHeight));
			}
			while (snakeBlocks.Contains(nextBlock));

			constrictBlocks = new Point[4];
			constrictBlocks[0] = new Point(nextBlock.X, nextBlock.Y + 1);
			constrictBlocks[1] = new Point(nextBlock.X, nextBlock.Y - 1);
			constrictBlocks[2] = new Point(nextBlock.X + 1, nextBlock.Y);
			constrictBlocks[3] = new Point(nextBlock.X - 1, nextBlock.Y);
		}

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);

			sfScore = Content.Load<SpriteFont>("ScoreFont");
			sfSplash = Content.Load<SpriteFont>("SplashFont");

			Vector2 tmpVec = sfSplash.MeasureString("SNAKE");
			splashVec = new Vector2((graphics.GraphicsDevice.Viewport.Width / 2.0f) - (tmpVec.X / 2.0f), 0);

			tmpVec = sfScore.MeasureString("Press 'SpaceBar' to Play");
			enterVec = new Vector2((graphics.GraphicsDevice.Viewport.Width / 2.0f) - (tmpVec.X / 2.0f),
				graphics.GraphicsDevice.Viewport.Height - tmpVec.Y);

			blockTexture = Content.Load<Texture2D>("block");
			explode = Content.Load<ParticleEffect>("BasicExplosion");
			explode.LoadContent(Content);
			explode.Initialise();

			renderer.LoadContent(Content);
		}

		protected override void UnloadContent() { }

		private void GetKeyboardInput()
		{
			KeyboardState keyState = Keyboard.GetState();

			if (state == GameState.Splash)
			{
				if (keyState.IsKeyDown(Keys.Space) && dlgHiScore.Visible == false)
				{
					score = 0;
					state = GameState.Playing;
				}
				else if (keyState.IsKeyDown(Keys.Escape))
					Environment.Exit(0);
				return;
			}

			if (snakeDirection != Direction.Down && keyState.IsKeyDown(Keys.Up))
			{
				if (!keysDown.Contains(Keys.Up))
					nextSnakeDirection = Direction.Up;
			}
			else
				keysDown.Remove(Keys.Up);

			if (snakeDirection != Direction.Up && keyState.IsKeyDown(Keys.Down))
			{
				if (!keysDown.Contains(Keys.Down))
					nextSnakeDirection = Direction.Down;
			}
			else
				keysDown.Remove(Keys.Down);

			if (snakeDirection != Direction.Right && keyState.IsKeyDown(Keys.Left))
			{
				if (!keysDown.Contains(Keys.Left))
					nextSnakeDirection = Direction.Left;
			}
			else
				keysDown.Remove(Keys.Left);

			if (snakeDirection != Direction.Left && keyState.IsKeyDown(Keys.Right))
			{
				if (!keysDown.Contains(Keys.Right))
					nextSnakeDirection = Direction.Right;
			}
			else
				keysDown.Remove(Keys.Right);
		}

		private void DoCollisions()
		{
			Point head = snakeBlocks[0];
			Point nextHead = Point.Zero;

			// are we colliding with one of the level sides?
			switch (nextSnakeDirection)
			{
				case Direction.Up:
					nextHead = new Point(head.X, head.Y - 1);
					break;
				case Direction.Down:
					nextHead = new Point(head.X, head.Y + 1);
					break;
				case Direction.Left:
					nextHead = new Point(head.X - 1, head.Y);
					break;
				case Direction.Right:
					nextHead = new Point(head.X + 1, head.Y);
					break;
			}

			if (nextHead.X < 0 || nextHead.Y < 0 ||
				nextHead.X >= levelWidth || nextHead.Y >= levelHeight)
			{
				hurt.Play(0.6f, RandomPitch(0.2f), 0.0f);
				state = GameState.Dying;
				stepTime /= 4;
				return;
			}

			// are we colliding with ourself
			if (snakeBlocks.GetRange(1, snakeBlocks.Count - 2).Contains(nextHead))
			{
				hurt.Play(0.6f, RandomPitch(0.2f), 0.0f);
				state = GameState.Dying;
				stepTime /= 4;
				return;
			}
		}

		private void DoHighScore()
		{
			if (!highScores.isHighScore(score))
				return;

			dlgHiScore.ShowDialog();

			if (dlgHiScore.DialogResult == System.Windows.Forms.DialogResult.Cancel)
				return;

			HighScore hs = new HighScore(dlgHiScore.PlayerName, score);
			highScores.Add(hs);

			XmlWriter writer = XmlWriter.Create(System.Environment.CurrentDirectory + "\\HighScores.xml");
			xmler.Serialize(writer, highScores);
			writer.Close();
		}

		private float RandomPitch(float deviance)
		{
			float r = (float)rand.NextDouble();
			bool neg = rand.Next(1) == 1;

			return r * deviance * (neg ? 1 : -1);
		}

		private void Step()
		{
			if (state == GameState.Dying)
			{
				snakeBlocks.RemoveAt(snakeBlocks.Count - 1);

				if (snakeBlocks.Count == 0)
				{
					Vector2 expVec = ScreenVec(nextBlock);
					expVec.X += (float)blockSize / 2.0f;
					expVec.Y += (float)blockSize / 2.0f;
					boom.Play(0.5f, RandomPitch(0.5f), 0.0f);
					explode.Trigger(ref expVec);

					state = GameState.Splash;
					InitSnake();
					DoHighScore();

					foreach (HighScore s in highScores)
						Console.WriteLine(s);
				}

				return;
			}

			KeyboardState keyState = Keyboard.GetState();
			keysDown.Clear();
			keysDown.AddRange(keyState.GetPressedKeys());

			snakeDirection = nextSnakeDirection;
			Point head = snakeBlocks[0];
			Point next = new Point(head.X + (snakeDirection == Direction.Right ? 1 : 0) -
											(snakeDirection == Direction.Left ? 1 : 0),
								   head.Y + (snakeDirection == Direction.Down ? 1 : 0) -
											(snakeDirection == Direction.Up ? 1 : 0));


			// check for constrict
			if (!constrictBlocks.Except(snakeBlocks).Any())
			{
				score += 5;
				stepTime -= 3;
				Vector2 screenExp = ScreenVec(nextBlock);
				Vector2 expVec = new Vector2(screenExp.X + (blockSize / 2.0f),
					screenExp.Y + (blockSize / 2.0f));
				explode.Trigger(ref expVec);
				boom.Play(0.5f, RandomPitch(0.5f), 0.0f);
				GenerateBlock();
			}

			snakeBlocks.Insert(0, next);
			if (next.Equals(nextBlock))
			{
				crunch.Play(1.0f, RandomPitch(0.3f), 0.0f);
				score++;
				stepTime -= 3;
				GenerateBlock();
			}
			else // Pop the last block off
			{
				snakeBlocks.RemoveAt(snakeBlocks.Count - 1);
			}
		}

		protected override void Update(GameTime gameTime)
		{
			explode.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

			// Allows the game to exit
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
				this.Exit();

			GetKeyboardInput();
			
			// Step
			timeSinceLastStep += gameTime.ElapsedGameTime.Milliseconds;
			if (timeSinceLastStep >= stepTime)
			{
				if (state == GameState.Playing)
					DoCollisions();

				if (state == GameState.Playing || state == GameState.Dying)
					Step();

				timeSinceLastStep = 0;
			}

			base.Update(gameTime);
		}

		private Vector2 ScreenVec(Point p)
		{
			//return new Vector2(
			//	(graphics.GraphicsDevice.Viewport.Width / levelWidth) * p.X,
			//	(graphics.GraphicsDevice.Viewport.Height / levelHeight) * p.Y);
			return new Vector2(p.X * blockSize, p.Y * blockSize);
		}

		private Rectangle ScreenRect(Point p)
		{
			//float screenWidth = graphics.GraphicsDevice.Viewport.Width / levelWidth;
			//float screenHeight = graphics.GraphicsDevice.Viewport.Height / levelHeight;
			//return new Rectangle((int)(screenWidth * p.X), (int)(screenHeight * p.Y),
			//	(int)screenWidth, (int)screenHeight);
			return new Rectangle(p.X * blockSize, p.Y * blockSize, blockSize, blockSize);
		}

		private void DrawText()
		{
			switch (state)
			{
				case GameState.Splash:
					spriteBatch.DrawString(sfSplash, "SNAKE", splashVec, Color.White);
					spriteBatch.DrawString(sfScore, "Press 'SpaceBar' to Play", enterVec, Color.White);

					float border = 76;
					float scoreY = 128;
					foreach (HighScore hiScore in highScores)
					{
						Vector2 scoreVec = new Vector2((graphics.GraphicsDevice.Viewport.Width - border)-
							sfScore.MeasureString(hiScore.Score.ToString()).X, scoreY);
						spriteBatch.DrawString(sfScore, hiScore.Score.ToString(), scoreVec, Color.LightGray);
						spriteBatch.DrawString(sfScore, hiScore.Name, new Vector2(border, scoreY), Color.LightGray);
						scoreY += sfScore.MeasureString("asdf").Y;
					}
					break;
				default:
					spriteBatch.DrawString(sfScore, "Score: " + score.ToString(), new Vector2(5, 0), Color.White);
					break;
			}
		}

		private void DrawGame()
		{
			switch (state)
			{
				case GameState.Playing:
					// Draw the next block
					spriteBatch.Draw(blockTexture, ScreenRect(nextBlock), Color.GreenYellow);

					// Draw snake
					foreach (Point block in snakeBlocks)
					{
						if (block.Equals(snakeBlocks[0]))
							spriteBatch.Draw(blockTexture, ScreenRect(block), Color.White);
						else
							spriteBatch.Draw(blockTexture, ScreenRect(block), Color.Gray);
					}
					break;
				case GameState.Dying:
					// Draw the next block
					spriteBatch.Draw(blockTexture, ScreenRect(nextBlock), Color.GreenYellow);

					// Draw snake
					foreach (Point block in snakeBlocks)
					{
						if (block.Equals(snakeBlocks[0]))
							spriteBatch.Draw(blockTexture, ScreenRect(block), Color.Red);
						else
							spriteBatch.Draw(blockTexture, ScreenRect(block), Color.Gray);
					}
					break;
				case GameState.HiScore:
					break;
			}
		}

		private void DrawParticles()
		{
			//if (state == GameState.Splash)
				renderer.RenderEffect(explode);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);
			spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

			DrawGame();
			spriteBatch.End();
			base.Draw(gameTime);

			DrawParticles();

			spriteBatch.Begin();
			DrawText();
			spriteBatch.End();
		}
	}
}
