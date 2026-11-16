'use client';

import { useEffect, useEffectEvent, useRef, useState } from 'react';

import { Pause, Play } from 'lucide-react';

import { Button } from '@components/ui/button';

export default function LoginVideo() {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [playing, setPlaying] = useState(false);

  const syncPlayback = useEffectEvent((preference: MediaQueryList | MediaQueryListEvent) => {
    const video = videoRef.current;
    if (!video) return;

    if (preference.matches) {
      video.pause();
    } else {
      void video.play().catch(() => {});
    }
  });

  useEffect(() => {
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
    syncPlayback(preference);
    preference.addEventListener('change', syncPlayback);
    return () => preference.removeEventListener('change', syncPlayback);
  }, []);

  function togglePlayback() {
    const video = videoRef.current;
    if (!video) return;

    if (video.paused) {
      video.play();
    } else {
      video.pause();
    }
  }

  return (
    <figure className="mx-auto hidden w-full max-w-[min(640px,calc((100dvh-152px)*0.75))] lg:block">
      <div className="relative aspect-3/4 overflow-hidden rounded-[24px] bg-neutral-900">
        <video
          ref={videoRef}
          muted
          loop
          playsInline
          preload="metadata"
          poster="/videos/mq-9-reaper-poster.jpg"
          onPlay={() => setPlaying(true)}
          onPause={() => setPlaying(false)}
          aria-label="MQ-9 Reaper military drone footage"
          className="absolute inset-0 h-full w-full object-cover"
        >
          <source src="/videos/mq-9-reaper.mp4" type="video/mp4" />
        </video>
        <div className="pointer-events-none absolute inset-0 bg-linear-to-t from-black/80 via-black/5 to-black/15" />
        <div className="absolute right-6 bottom-6 left-6 flex items-end justify-end gap-4 text-white sm:right-8 sm:bottom-8 sm:left-8">
          <Button
            variant="ghost"
            size="icon"
            onClick={togglePlayback}
            aria-label={playing ? 'Pause drone video' : 'Play drone video'}
            className="size-10 rounded-full border border-white/25 bg-black/20 text-white hover:bg-white/20 hover:text-white"
          >
            {playing ? <Pause /> : <Play />}
          </Button>
        </div>
      </div>
    </figure>
  );
}
