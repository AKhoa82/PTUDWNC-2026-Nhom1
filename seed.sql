DO $$
DECLARE
    cat_id uuid;
    rec_id uuid;

    i int;
    j int;
    k int;

    cat_name text;
    cat_slug text;

    rec_title text;
    rec_slug text;

    ingredient_names text[];
    step_titles text[];
    step_descriptions text[];

    categories text[] := ARRAY[
        'Món nướng',
        'Món xào',
        'Món hấp',
        'Món chiên',
        'Món sốt',
        'Món canh',
        'Món lẩu',
        'Món trộn',
        'Món kho',
        'Món om',
        'Món gỏi',
        'Món súp',
        'Món quay',
        'Món rim',
        'Món sốt vang',
        'Món bóp thấu',
        'Món tái',
        'Món nộm',
        'Món tráng miệng',
        'Đồ uống'
    ];

    recipes text[] := ARRAY[
        'Cá lóc nướng trui',
        'Mực xào sa tế',
        'Cá lóc hấp gừng',
        'Cánh gà chiên giòn',
        'Bò sốt tiêu đen',
        'Canh chua hải sản',
        'Lẩu Thái thập cẩm',
        'Gà xé trộn thính',
        'Thịt kho tàu',
        'Bắp bò om sấu'
    ];

BEGIN

    -- ============================================================
    -- 1. TẠO 20 CATEGORY
    -- ============================================================

    FOR i IN 1..20 LOOP

        cat_name := categories[i];

        cat_slug := 'danh-muc-' || i;

        SELECT "Id"
        INTO cat_id
        FROM "Categories"
        WHERE "Slug" = cat_slug
        LIMIT 1;

        IF cat_id IS NULL THEN
            cat_id := gen_random_uuid();

            INSERT INTO "Categories"
            (
                "Id",
                "Name",
                "Slug",
                "CreatedAt"
            )
            VALUES
            (
                cat_id,
                cat_name,
                cat_slug,
                NOW()
            );
        END IF;


        -- ========================================================
        -- 2. MỖI CATEGORY TẠO 5 RECIPE
        -- ========================================================

        FOR j IN 1..5 LOOP

            rec_id := gen_random_uuid();

            -- Lấy tên món từ mảng 10 món, lặp lại theo Category
            rec_title :=
                recipes[((i + j - 2) % array_length(recipes, 1)) + 1];

            rec_slug :=
                'mon-an-' || i || '-' || j;


            -- ====================================================
            -- TẠO RECIPE
            -- ====================================================

            INSERT INTO "Recipes"
            (
                "Id",
                "Title",
                "Slug",
                "Description",
                "PrepTimeMinutes",
                "CookingTimeMinutes",
                "Servings",
                "Difficulty",
                "Instructions",
                "Status",
                "CategoryId",
                "CreatedAt"
            )
            VALUES
            (
                rec_id,

                rec_title,

                rec_slug,

                'Món ăn thơm ngon được chế biến theo cách đơn giản, phù hợp cho bữa cơm gia đình.',

                15,

                30,

                4,

                1,

                'Chuẩn bị đầy đủ nguyên liệu, sơ chế sạch sẽ và thực hiện lần lượt các bước theo hướng dẫn để có món ăn thơm ngon.',

                1,

                cat_id,

                NOW()
            );


            -- ====================================================
            -- 3. TẠO 10 INGREDIENTS
            -- ====================================================

            ingredient_names := ARRAY[
                'Nguyên liệu chính',
                'Hành tím',
                'Tỏi',
                'Gừng',
                'Ớt',
                'Hành lá',
                'Nước mắm',
                'Đường',
                'Muối',
                'Tiêu'
            ];


            FOR k IN 1..10 LOOP

                INSERT INTO "RecipeIngredients"
                (
                    "Id",
                    "RecipeId",
                    "Name",
                    "Quantity",
                    "Unit",
                    "SortOrder"
                )
                VALUES
                (
                    gen_random_uuid(),

                    rec_id,

                    ingredient_names[k],

                    CASE
                        WHEN k = 1 THEN 500
                        WHEN k = 2 THEN 20
                        WHEN k = 3 THEN 15
                        WHEN k = 4 THEN 20
                        WHEN k = 5 THEN 10
                        WHEN k = 6 THEN 20
                        WHEN k = 7 THEN 30
                        WHEN k = 8 THEN 10
                        WHEN k = 9 THEN 5
                        WHEN k = 10 THEN 5
                    END,

                    CASE
                        WHEN k = 1 THEN 'gram'
                        WHEN k = 2 THEN 'gram'
                        WHEN k = 3 THEN 'gram'
                        WHEN k = 4 THEN 'gram'
                        WHEN k = 5 THEN 'gram'
                        WHEN k = 6 THEN 'gram'
                        WHEN k = 7 THEN 'ml'
                        WHEN k = 8 THEN 'gram'
                        WHEN k = 9 THEN 'gram'
                        WHEN k = 10 THEN 'gram'
                    END,

                    k
                );

            END LOOP;


            -- ====================================================
            -- 4. TẠO 5 BƯỚC NẤU ĂN
            -- ====================================================

            step_titles := ARRAY[
                'Sơ chế nguyên liệu',
                'Ướp nguyên liệu',
                'Chuẩn bị gia vị',
                'Chế biến món ăn',
                'Hoàn thành và thưởng thức'
            ];


            step_descriptions := ARRAY[
                'Rửa sạch các nguyên liệu, sơ chế và cắt thái theo kích thước phù hợp.',

                'Cho nguyên liệu chính vào tô, thêm gia vị và trộn đều. Ướp trong khoảng 10 đến 15 phút để nguyên liệu thấm đều.',

                'Băm nhỏ hành, tỏi và chuẩn bị các loại gia vị cần thiết cho quá trình chế biến.',

                'Tiến hành nấu, xào, hấp, chiên hoặc chế biến nguyên liệu theo phương pháp phù hợp cho món ăn.',

                'Kiểm tra độ chín và nêm nếm lại cho vừa khẩu vị. Trình bày món ăn ra đĩa và thưởng thức khi còn nóng.'
            ];


            FOR k IN 1..5 LOOP

                INSERT INTO "RecipeSteps"
                (
                    "Id",
                    "RecipeId",
                    "StepNumber",
                    "Title",
                    "Description",
                    "DurationMinutes"
                )
                VALUES
                (
                    gen_random_uuid(),

                    rec_id,

                    k,

                    step_titles[k],

                    step_descriptions[k],

                    CASE
                        WHEN k = 1 THEN 5
                        WHEN k = 2 THEN 10
                        WHEN k = 3 THEN 5
                        WHEN k = 4 THEN 20
                        WHEN k = 5 THEN 5
                    END
                );

            END LOOP;

        END LOOP;

    END LOOP;

END $$;