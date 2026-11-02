/** Supplied iOS launch images, matched by device size, pixel ratio, and orientation. */
const SPLASH_SCREENS = [
  [320, 568, 2, 'landscape', 'apple-splash-1136-640.png'],
  [320, 568, 2, 'portrait', 'apple-splash-640-1136.png'],
  [375, 667, 2, 'landscape', 'apple-splash-1334-750.png'],
  [375, 667, 2, 'portrait', 'apple-splash-750-1334.png'],
  [375, 812, 3, 'landscape', 'apple-splash-2436-1125.png'],
  [375, 812, 3, 'portrait', 'apple-splash-1125-2436.png'],
  [390, 844, 3, 'landscape', 'apple-splash-2532-1170.png'],
  [390, 844, 3, 'portrait', 'apple-splash-1170-2532.png'],
  [393, 852, 3, 'landscape', 'apple-splash-2556-1179.png'],
  [393, 852, 3, 'portrait', 'apple-splash-1179-2556.png'],
  [402, 874, 3, 'landscape', 'apple-splash-2622-1206.png'],
  [402, 874, 3, 'portrait', 'apple-splash-1206-2622.png'],
  [414, 736, 3, 'landscape', 'apple-splash-2208-1242.png'],
  [414, 736, 3, 'portrait', 'apple-splash-1242-2208.png'],
  [414, 896, 3, 'landscape', 'apple-splash-2688-1242.png'],
  [414, 896, 2, 'landscape', 'apple-splash-1792-828.png'],
  [414, 896, 3, 'portrait', 'apple-splash-1242-2688.png'],
  [414, 896, 2, 'portrait', 'apple-splash-828-1792.png'],
  [420, 912, 3, 'landscape', 'apple-splash-2736-1260.png'],
  [420, 912, 3, 'portrait', 'apple-splash-1260-2736.png'],
  [428, 926, 3, 'landscape', 'apple-splash-2778-1284.png'],
  [428, 926, 3, 'portrait', 'apple-splash-1284-2778.png'],
  [430, 932, 3, 'landscape', 'apple-splash-2796-1290.png'],
  [430, 932, 3, 'portrait', 'apple-splash-1290-2796.png'],
  [440, 956, 3, 'landscape', 'apple-splash-2868-1320.png'],
  [440, 956, 3, 'portrait', 'apple-splash-1320-2868.png'],
  [466, 678, 3, 'landscape', 'apple-splash-2034-1398.png'],
  [466, 678, 3, 'portrait', 'apple-splash-1398-2034.png'],
  [744, 1133, 2, 'landscape', 'apple-splash-2266-1488.png'],
  [744, 1133, 2, 'portrait', 'apple-splash-1488-2266.png'],
  [768, 1024, 2, 'landscape', 'apple-splash-2048-1536.png'],
  [768, 1024, 2, 'portrait', 'apple-splash-1536-2048.png'],
  [810, 1080, 2, 'landscape', 'apple-splash-2160-1620.png'],
  [810, 1080, 2, 'portrait', 'apple-splash-1620-2160.png'],
  [820, 1180, 2, 'landscape', 'apple-splash-2360-1640.png'],
  [820, 1180, 2, 'portrait', 'apple-splash-1640-2360.png'],
  [834, 1112, 2, 'landscape', 'apple-splash-2224-1668.png'],
  [834, 1112, 2, 'portrait', 'apple-splash-1668-2224.png'],
  [834, 1194, 2, 'landscape', 'apple-splash-2388-1668.png'],
  [834, 1194, 2, 'portrait', 'apple-splash-1668-2388.png'],
  [834, 1210, 2, 'landscape', 'apple-splash-2420-1668.png'],
  [834, 1210, 2, 'portrait', 'apple-splash-1668-2420.png'],
  [890, 626, 3, 'landscape', 'apple-splash-1878-2670.png'],
  [890, 626, 3, 'portrait', 'apple-splash-2670-1878.png'],
  [1024, 1366, 2, 'landscape', 'apple-splash-2732-2048.png'],
  [1024, 1366, 2, 'portrait', 'apple-splash-2048-2732.png'],
  [1032, 1376, 2, 'landscape', 'apple-splash-2752-2064.png'],
  [1032, 1376, 2, 'portrait', 'apple-splash-2064-2752.png'],
] as const;

export default function AppleSplashScreens() {
  return (
    <>
      {SPLASH_SCREENS.map(([width, height, ratio, orientation, filename]) => (
        <link
          key={filename}
          rel="apple-touch-startup-image"
          media={`(device-width: ${width}px) and (device-height: ${height}px) and (-webkit-device-pixel-ratio: ${ratio}) and (orientation: ${orientation})`}
          href={`/splash/${filename}`}
        />
      ))}
    </>
  );
}
