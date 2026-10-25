import os
from PIL import Image, ImageDraw, ImageFont

def create_chrome_header(width, url, title="CMS — Kiểm soát chi phí", is_incognito=True):
    header_height = 84
    img = Image.new("RGB", (width, header_height), "#202124")
    draw = ImageDraw.Draw(img)
    
    try:
        font_tab = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
        font_url = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 13)
        font_ui = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 14)
        font_sm = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 11)
    except Exception:
        font_tab = ImageFont.load_default()
        font_url = ImageFont.load_default()
        font_ui = ImageFont.load_default()
        font_sm = ImageFont.load_default()

    # --- ROW 1: TABS (height: 38px) ---
    tab_bg = "#323639"
    tab_w = min(240, width // 4)
    tab_h = 32
    tab_y = 6
    
    # Active tab pill
    draw.rounded_rectangle([8, tab_y, 8 + tab_w, tab_y + tab_h], radius=8, fill=tab_bg)
    # Tab favicon (small blue square)
    draw.rounded_rectangle([18, tab_y + 9, 32, tab_y + 23], radius=3, fill="#1677e8")
    draw.text((22, tab_y + 9), "C", fill="#ffffff", font=font_sm)
    # Tab title
    draw.text((38, tab_y + 8), title, fill="#f1f3f4", font=font_tab)
    # Close tab 'x'
    draw.text((8 + tab_w - 20, tab_y + 7), "×", fill="#9aa0a6", font=font_ui)
    
    # New tab '+'
    draw.text((8 + tab_w + 12, tab_y + 7), "+", fill="#9aa0a6", font=font_ui)
    
    # Window controls (top right)
    draw.text((width - 90, 8), "—", fill="#9aa0a6", font=font_ui)
    draw.text((width - 55, 8), "□", fill="#9aa0a6", font=font_ui)
    draw.text((width - 25, 8), "×", fill="#9aa0a6", font=font_ui)

    # --- ROW 2: OMNIBOX / ADDRESS BAR (height: 46px, y: 38 to 84) ---
    draw.rectangle([0, 38, width, header_height], fill="#323639")
    
    # Nav buttons (Back, Forward, Refresh)
    draw.text((14, 48), "←", fill="#9aa0a6", font=font_ui)
    draw.text((42, 48), "→", fill="#5f6368", font=font_ui) # inactive forward
    draw.text((70, 48), "↻", fill="#9aa0a6", font=font_ui)
    
    # Address bar pill
    bar_x1 = 100
    bar_x2 = width - 110
    bar_y1 = 44
    bar_y2 = 76
    draw.rounded_rectangle([bar_x1, bar_y1, bar_x2, bar_y2], radius=16, fill="#202124", outline="#5f6368", width=1)
    
    # Lock / Secure icon inside address bar
    draw.text((bar_x1 + 12, bar_y1 + 6), "🔒", fill="#9aa0a6", font=font_tab)
    
    # URL text
    # Display full URL
    display_url = url
    if display_url.startswith("https://"):
        draw.text((bar_x1 + 34, bar_y1 + 7), "https://", fill="#9aa0a6", font=font_url)
        prefix_w = draw.textlength("https://", font=font_url) if hasattr(draw, "textlength") else 50
        domain_part = display_url[8:].split("/")[0]
        path_part = display_url[8 + len(domain_part):]
        draw.text((bar_x1 + 34 + prefix_w, bar_y1 + 7), domain_part, fill="#ffffff", font=font_url)
        domain_w = draw.textlength(domain_part, font=font_url) if hasattr(draw, "textlength") else 120
        draw.text((bar_x1 + 34 + prefix_w + domain_w, bar_y1 + 7), path_part, fill="#9aa0a6", font=font_url)
    else:
        draw.text((bar_x1 + 34, bar_y1 + 7), display_url, fill="#ffffff", font=font_url)
        
    # Bookmark star
    draw.text((bar_x2 - 28, bar_y1 + 6), "☆", fill="#9aa0a6", font=font_ui)
    
    # Incognito badge or User profile on the right
    if is_incognito:
        draw.rounded_rectangle([width - 95, 48, width - 15, 74], radius=13, fill="#202124", outline="#5f6368")
        draw.text((width - 85, 52), "🕵 Incognito", fill="#e8eaed", font=font_sm)
    else:
        draw.ellipse([width - 45, 48, width - 20, 73], fill="#1677e8")
        draw.text((width - 38, 51), "QT", fill="#ffffff", font=font_sm)
        
    # Bottom subtle border
    draw.line([0, header_height - 1, width, header_height - 1], fill="#202124")
    
    return img

def attach_frame_to_image(raw_image_path, output_image_path, url, title="CMS — Kiểm soát chi phí"):
    raw = Image.open(raw_image_path)
    w, h = raw.size
    header = create_chrome_header(w, url, title, is_incognito=True)
    
    total_h = h + header.height
    combined = Image.new("RGB", (w, total_h))
    combined.paste(header, (0, 0))
    combined.paste(raw, (0, header.height))
    combined.save(output_image_path, "PNG", optimize=True)
    print(f"Saved: {output_image_path} ({w}x{total_h})")

if __name__ == "__main__":
    os.makedirs("docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots", exist_ok=True)
    
    items = [
        (
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots_uat/01_rate_card_detail_published_v1_with_new_version_cta_raw.png",
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/01_rate_card_detail_published_v1_with_new_version_cta.png",
            "https://cms.lsslogistics.vn/rate-cards/01a13820-4e67-793a-a293-e64636c62dc5",
            "CMS — Bảng giá TDNT SGN-HAN"
        ),
        (
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots_uat/02_appendices_list_separated_cta_and_row_actions_raw.png",
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/02_appendices_list_separated_cta_and_row_actions.png",
            "https://cms.lsslogistics.vn/rate-cards/appendices",
            "CMS — Quản lý phụ lục giá"
        ),
        (
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots_uat/03_rate_card_v2_draft_edit_form_prefilled_raw.png",
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/03_rate_card_v2_draft_edit_form_prefilled.png",
            "https://cms.lsslogistics.vn/rate-cards/01a13820-4e67-793a-a293-e64636c62dc5/edit",
            "CMS — Sửa bản nháp v2 TDNT_SGN_HAN"
        ),
        (
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots_uat/04_rate_card_detail_with_v2_draft_raw.png",
            "docs/po/24102026-LCMS_RATE_VERSION_UI_01_DEV_Fix_v1.1/screenshots/04_rate_card_v2_draft_edit_band_cw120_save_draft.png",
            "https://cms.lsslogistics.vn/rate-cards/01a13820-4e67-793a-a293-e64636c62dc5",
            "CMS — Bảng giá TDNT SGN-HAN (v2 nháp)"
        ),
    ]
    
    for raw_p, out_p, url, title in items:
        if os.path.exists(raw_p):
            attach_frame_to_image(raw_p, out_p, url, title)
