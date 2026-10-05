"""Turns the harness frame sequences (clip_<name>/0000.png ...) into the store page GIFs.

Run:  python make_gifs.py <shots folder>      (needs ffmpeg on the PATH)
Writes description/gif_<name>.gif. Clips are captured at 30 fps; the GIFs keep every `step`-th frame.
Where the clip was shot at an even pixel scale the frame is halved exactly (nearest neighbour), so
the pixel art stays crisp. Steam shows the About section 780 px wide and plays at most 12 s.
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "description")

# name: (scene folder, clip folder, crop "w:h:x:y" in the 1920x1080 frames, output width, frame step, first, last)
CLIPS = {
    "leader": ("intro", "clip_leader", "1120:600:400:460", 1120, 2, 0, 149, "neighbor"),
    "doors": ("line", "clip_doors", "1920:1080:0:0", 960, 3, 0, 359, "lanczos"),
    "line": ("line", "clip_line", "1920:1080:0:0", 960, 2, 0, 239, "neighbor"),
    "factory": ("factory", "clip_factory", "1920:1080:0:0", 960, 2, 0, 239, "neighbor"),
    "city": ("late", "clip_city", "1920:1080:0:0", 960, 2, 0, 239, "neighbor"),
    "genetics": ("genetics", "clip_genetics", "1720:600:200:300", 960, 2, 0, 90, "lanczos"),
}


def make(src_dir, name, crop, width, step, first, last, flt="neighbor"):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "gif_" + name + ".gif")
    fps = 30 / step
    chain = (f"select='between(n\\,{first}\\,{last})*not(mod(n\\,{step}))',setpts=N/({fps}*TB),"
             f"crop={crop},scale={width}:-2:flags={flt}")
    graph = (f"[0:v]{chain},split[a][b];"
             f"[a]palettegen=max_colors=128:stats_mode=diff[p];"
             f"[b][p]paletteuse=dither=none:diff_mode=rectangle")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-framerate", "30", "-i", os.path.join(src_dir, "%04d.png"),
                    "-filter_complex", graph, "-r", str(fps), "-loop", "0", path], check=True)
    print(f"{path}  {os.path.getsize(path) / 1e6:.1f} MB")


if __name__ == "__main__":
    root = sys.argv[1]
    only = sys.argv[2:]
    for name, (scene, clip, crop, w, step, a, b, flt) in CLIPS.items():
        if only and name not in only:
            continue
        d = os.path.join(root, scene, clip)
        if os.path.isdir(d):
            make(d, name, crop, w, step, a, b, flt)
