using System;

namespace ca_water_prototype
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (CAWater game = new CAWater())
            {
                game.Run();
            }
        }
    }
#endif
}

